using System.Text.Json;

namespace CaminoALaGloria.Api;

public sealed class SimulationEngine
{
    private readonly WorldCatalog _world;
    private readonly EventCatalog _events;

    public SimulationEngine(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "world.json");
        _world = JsonSerializer.Deserialize<WorldCatalog>(File.ReadAllText(path), JsonOptions) ?? new WorldCatalog();
        var eventsPath = Path.Combine(environment.ContentRootPath, "Data", "events.json");
        _events = JsonSerializer.Deserialize<EventCatalog>(File.ReadAllText(eventsPath), JsonOptions) ?? new EventCatalog();
        AddExpandedEventCatalog();
        if (_world.Clubs.Count == 0)
        {
            _world.Clubs = _world.Leagues.SelectMany(league => league.ClubNames.Select((name, index) => new Club
            {
                Name = name, Nickname = name, League = league.Name, Region = league.Region,
                Prestige = Math.Max(35, league.Prestige - 12 + (index % 18))
            })).ToList();
        }
        AddGlobalWorldCatalog();
    }

    public WorldCatalog World() => _world;

    public CareerState Create(CreateCareerRequest request)
    {
        var club = _world.Clubs.FirstOrDefault(c => c.Name == request.Club && c.League == request.League)
            ?? _world.Clubs.FirstOrDefault(c => c.League == request.League) ?? _world.Clubs.First();
        var state = new CareerState
        {
            RandomState = (uint)HashCode.Combine(request.Name, request.Position, DateTime.UtcNow.Ticks),
            WorldSeed = (uint)HashCode.Combine(request.Name, request.Club, request.League, "world-2026"),
            CurrentClub = club.Name, CurrentLeague = club.League,
            Player = new Player
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Promesa" : request.Name.Trim(),
                Nationality = _world.Nationalities.Contains(request.Nationality) ? request.Nationality : _world.Nationalities.First(),
                ShirtNumber = Math.Clamp(request.ShirtNumber, 1, 99), Position = request.Position,
                Archetype = request.Archetype, Personality = request.Personality,
                Overall = StartingOverall(request.Position), Potential = StartingPotential(request.Archetype)
            }
        };
        state.NationalTeam = NationalTeamFor(state.Player.Nationality);
        state.WorldClubs = BuildWorldProfiles(state);
        InitializeAttributes(state.Player, state);
        InitializeSeason(state);
        state.Contract = CreateContract(state, club, "Cantera");
        RefreshMarketProfile(state);
        RefreshObjectives(state);
        AddLedger(state, "Firma", $"Bono de firma con {club.Name}", state.Contract.SigningBonusEur);
        state.Timeline.Add($"Firmaste tu primer contrato con {club.Name} en {club.League}.");
        state.ActiveEvent = CreatePreseasonEvent(state);
        return state;
    }

    public CareerState AdvanceToNextEvent(CareerState state)
    {
        if (state.IsRetired) throw new InvalidOperationException("La carrera ya finalizo con el retiro del jugador.");
        RefreshMarketProfile(state);
        RefreshObjectives(state);
        if (state.ActiveEvent is not null) throw new InvalidOperationException("Resuelve la situación activa antes de avanzar.");
        if (state.SeasonComplete) throw new InvalidOperationException("La temporada terminó. Revísala antes de comenzar la siguiente.");

        var league = League(state);
        while (state.CurrentMatchday < MatchdayCount(state))
        {
            state.CurrentMatchday++;
            PaySalaryUntil(state, Math.Min(12, 1 + state.CurrentMatchday * 12 / Math.Max(1, MatchdayCount(state))));
            var fixtures = state.Fixtures.Where(f => f.Matchday == state.CurrentMatchday).ToList();
            var playerFixture = fixtures.FirstOrDefault(f => f.Home == state.CurrentClub || f.Away == state.CurrentClub);
            foreach (var fixture in fixtures.Where(f => f != playerFixture)) ResolveBackgroundFixture(state, fixture);
            if (playerFixture is null) continue;

            if (ShouldCreateMatchEvent(state, playerFixture))
            {
                state.ActiveEvent = CreateMatchEvent(state, playerFixture);
                state.Timeline.Add($"Jornada {state.CurrentMatchday}: {state.ActiveEvent.Title}.");
                return state;
            }
            ResolvePlayerFixture(state, playerFixture, null, false);
            UpdateTable(state);
            var tournamentEvent = CreateTournamentKeyEvent(state);
            if (tournamentEvent is not null)
            {
                state.ActiveEvent = tournamentEvent;
                return state;
            }
        }

        PaySalaryUntil(state, 12);
        state.SeasonComplete = true;
        UpdateTable(state);
        var row = state.Table.FindIndex(r => r.Club == state.CurrentClub) + 1;
        state.Timeline.Add($"Finalizó la fase regular: {state.CurrentClub} terminó {row}º en {league.Name}.");
        return state;
    }

    public CareerState Decide(CareerState state, string optionId, MiniGameSubmission? miniGame = null)
    {
        if (state.ActiveEvent is null) throw new InvalidOperationException("No hay una decisión activa.");
        var active = state.ActiveEvent;
        if (active.Category == "Transferencias") return ResolveTransfer(state, optionId);
        if (active.Category == "Contrato") return ResolveContract(state, optionId);
        if (active.Category == "Pretemporada") return ResolvePreseason(state, active, optionId);
        var option = active.Options.FirstOrDefault(x => x.Id == optionId)
            ?? throw new InvalidOperationException("La opción no pertenece a la situación activa.");
        if (active.Match is null)
        {
            if (active.Challenge is not null) return ResolveSpecialMiniGame(state, active, option, miniGame);
            return ResolveOffPitchEvent(state, active, option);
        }

        var success = active.Challenge is not null
            ? ResolveMiniGame(state, active.Challenge, miniGame)
            : Next(state) < MatchActionSuccessChance(state, active, option);
        var fixture = state.Fixtures.First(f => f.Id == active.Match.FixtureId);
        ResolvePlayerFixture(state, fixture, active, success, option);
        active.Outcome = DescribeMatchOutcome(state, active, option, success, fixture);
        active.Resolution = MatchResolution(state, active, option, success, fixture);
        state.LastResolution = active.Resolution;
        if (active.Challenge is not null) state.SeasonMiniGameUsed = true;
        state.CompletedEvents.Add(active); RecordTemplate(state, active.TemplateId ?? active.Id, active.Match?.Rival); state.Timeline.Add(active.Outcome); state.EventIndex++;
        ResolveTournamentKeyMoment(state, fixture, success);
        state.ActiveEvent = null; UpdateTable(state);

        RefreshMarketProfile(state);
        if (success && IsTransferWindow(state) && state.Player.Reputation >= 15)
        {
            state.TransferOffers = GenerateTransferOffers(state);
            if (state.TransferOffers.Count > 0) state.ActiveEvent = TransferEvent(state);
        }
        if (state.ActiveEvent is null) state.ActiveEvent = CreateTournamentKeyEvent(state);
        if (state.ActiveEvent is null) state.ActiveEvent = CreateOffPitchEvent(state, active, success);
        return state;
    }

    public CareerState CompleteSeason(CareerState state)
    {
        EnsureLifestyle(state);
        if (!state.SeasonComplete || state.ActiveEvent is not null) throw new InvalidOperationException("Termina los partidos y eventos pendientes antes de cerrar la temporada.");
        var position = state.Table.FindIndex(x => x.Club == state.CurrentClub) + 1;
        if (state.Lifestyle.RecurringCostPerSeason > 0 && state.Player.Money >= state.Lifestyle.RecurringCostPerSeason)
            AddLedger(state, "Mantenimiento", "Vivienda y lujos de temporada", -state.Lifestyle.RecurringCostPerSeason);
        else if (state.Lifestyle.RecurringCostPerSeason > 0)
            state.Timeline.Add("El mantenimiento de tus lujos se aplazó por saldo insuficiente.");
        state.Lifestyle.Inventory.Clear();
        var seasonTitles = new List<string>();
        if (position == 1)
        {
            var trophy = $"{state.CurrentLeague} {state.Season}";
            state.Trophies.Add(trophy);
            seasonTitles.Add(trophy);
            state.Timeline.Add($"¡Campeones! {state.CurrentClub} gana {trophy}.");
            if (state.Contract is not null) AddLedger(state, "Prima", "Prima por título", state.Contract.TitleBonusEur);
        }
        ResolveTournamentCampaigns(state, seasonTitles);
        var p = state.Player;
        SaveLastSeasonPerformance(state);
        ApplySeasonDevelopment(state, position, seasonTitles);
        SimulateInternationalFinals(state);
        p.Age++;
        var seasonAwards = DetermineIndividualAwards(state, position, seasonTitles);
        SimulateWorldSeason(state, position, seasonTitles);
        ApplyLeagueMovement(state, position);
        var playerClubProfile = state.WorldClubs.FirstOrDefault(x => x.Club == state.CurrentClub);
        if (playerClubProfile is not null) playerClubProfile.League = state.CurrentLeague;
        state.SeasonArchives.Add(new SeasonArchive
        {
            Summary = new SeasonSummary { Season = state.Season, Club = state.CurrentClub, League = state.CurrentLeague, Appearances = p.LastSeasonAppearances, Goals = p.LastSeasonGoals, Assists = p.LastSeasonAssists, Minutes = state.SeasonMinutes - state.SeasonStartMinutes, AnnualSalaryEur = state.Contract?.AnnualGrossEur ?? 0, MarketValueEur = state.MarketProfile.EstimatedValueEur, NationalAppearances = p.NationalAppearances - state.SeasonStartNationalAppearances, Average = p.LastSeasonAverage, StartOverall = state.SeasonStartOverall, EndOverall = p.Overall, OverallChange = p.LastSeasonOverallChange, Potential = p.Potential, DevelopmentTrajectory = p.DevelopmentTrajectory, DevelopmentNote = p.DevelopmentNote, Titles = seasonTitles, TournamentCampaigns = state.TournamentCampaigns.Select(CloneCampaign).ToList(), FinalPosition = position, Awards = seasonAwards, Role = state.ClubRole, Milestones = state.CareerMilestones.Where(x => x.StartsWith($"{state.Season}:")).ToList() },
            Events = state.CompletedEvents.ToList(), Timeline = state.Timeline.ToList(), Ledger = state.Ledger.Where(x => x.Season == state.Season).ToList()
        });
        if (p.Age >= 40)
        {
            state.IsRetired = true;
            state.RetirementSummary = $"{p.Name} se retiro a los {p.Age} anos: {p.Appearances} partidos, {p.Goals} goles, {p.Assists} asistencias y promedio {AverageRating(p):0.00}.";
            state.Timeline.Add(state.RetirementSummary);
            return state;
        }
        state.TransferOffers = GenerateTransferOffers(state);
        state.ActiveEvent = ContractEvent(state);
        return state;
    }

    private void InitializeSeason(CareerState state)
    {
        state.Fixtures = GenerateFixtures(state);
        state.TournamentCampaigns = BuildTournamentCampaigns(state);
        state.Table = BuildTable(state);
        state.SeasonEventTarget = 4 + NextInt(state, 2);
        state.SeasonMiniGameId = SelectSeasonMiniGame(state);
        state.SeasonMiniGameUsed = false;
        state.NationalEventUsed = false;
        state.WorldEventUsed = false;
        state.SalaryMonthsPaid = 0;
        state.ImportantMatchdays = SelectImportantMatchdays(state);
        state.SeasonStartAppearances = state.Player.Appearances;
        state.SeasonStartGoals = state.Player.Goals;
        state.SeasonStartAssists = state.Player.Assists;
        state.SeasonStartRatingTotal = state.Player.RatingTotal;
        state.SeasonStartMinutes = state.SeasonMinutes;
        state.SeasonStartNationalAppearances = state.Player.NationalAppearances;
        state.SeasonStartOverall = state.Player.Overall;
    }

    private List<TournamentCampaign> BuildTournamentCampaigns(CareerState state)
    {
        var league = League(state); var region = league.Region;
        var domestic = region switch { "Inglaterra" => "FA Cup", "España" => "Copa del Rey", "Japón" => "Emperor's Cup", "México" => "Copa MX", "Argentina" => "Copa Argentina", "Brasil" => "Copa do Brasil", "Colombia" => "Copa Colombia", "Estados Unidos" => "US Open Cup", _ => "Copa nacional" };
        var campaigns = new List<TournamentCampaign> { new() { Season = state.Season, Competition = domestic, Type = "Copa nacional", Format = "Eliminación directa · partido único", Qualified = true, Phase = "Ronda inicial", BestRound = "Ronda inicial" } };
        var club = Club(state.CurrentClub)!;
        var continental = region switch { "Inglaterra" or "España" => "UEFA Champions League", "México" or "Estados Unidos" => "Concacaf Champions Cup", "Argentina" or "Brasil" or "Colombia" => "Copa Libertadores", "Japón" => "AFC Champions League Elite", _ => "" };
        var qualifies = club.Prestige >= league.Prestige - 8 || state.Player.LastSeasonAverage >= 7.05 || state.Player.Reputation >= 45;
        if (!string.IsNullOrEmpty(continental)) campaigns.Add(new TournamentCampaign { Season = state.Season, Competition = continental, Type = "Continental", Format = "Fase liga/grupos y eliminatorias", Qualified = qualifies, Phase = qualifies ? "Fase de grupos" : "No clasificado", BestRound = qualifies ? "Fase de grupos" : "No clasificado" });
        return campaigns;
    }

    private void ResolveTournamentCampaigns(CareerState state, List<string> titles)
    {
        var strength = (Club(state.CurrentClub)?.Prestige ?? 50) + state.Player.Overall / 5 + state.Player.Form / 10;
        foreach (var campaign in state.TournamentCampaigns.Where(x => x.Qualified && x.Phase != "Eliminado"))
        {
            var run = Math.Clamp((int)Math.Floor((strength + NextInt(state, 35) - 52) / 12d), 0, campaign.Type == "Continental" ? 5 : 4);
            campaign.Played += 1 + run; campaign.Wins += Math.Max(0, run - 1); campaign.Goals += Math.Max(0, run + NextInt(state, 5));
            campaign.BestRound = run switch { 0 => "Ronda inicial", 1 => "Octavos", 2 => "Cuartos de final", 3 => "Semifinal", _ => "Final" };
            campaign.Phase = campaign.BestRound;
            campaign.KeyMatches = Enumerable.Range(0, Math.Min(3, campaign.Played)).Select(i => $"{campaign.Competition}: {campaign.BestRound} · partido {i + 1}").ToList();
            if (run >= (campaign.Type == "Continental" ? 5 : 4))
            {
                campaign.Champion = state.CurrentClub; campaign.Phase = "Campeón"; campaign.BestRound = "Campeón";
                campaign.PrizeEur = League(state).MarketScaleEur * (campaign.Type == "Continental" ? .04m : .012m);
                titles.Add($"{campaign.Competition} {state.Season}"); state.Trophies.Add(titles[^1]); AddLedger(state, "Prima", $"Premio por {campaign.Competition}", campaign.PrizeEur);
                state.Timeline.Add($"¡Campeones! {state.CurrentClub} gana {campaign.Competition}.");
            }
            else if (run >= 3) campaign.RunnerUp = "Rival simulado";
        }
    }

    private SeasonEvent? CreateTournamentKeyEvent(CareerState state)
    {
        var eligible = state.TournamentCampaigns
            .Where(x => x.Qualified && !x.KeyMomentPlayed && x.Champion is null)
            .OrderBy(x => x.Type == "Copa nacional" ? 0 : 1)
            .ToList();
        if (eligible.Count == 0) return null;
        var matchdays = Math.Max(1, MatchdayCount(state));
        var campaign = eligible.FirstOrDefault(x => state.CurrentMatchday >= (x.Type == "Copa nacional" ? matchdays / 3 : matchdays * 2 / 3));
        if (campaign is null) return null;

        var ownPrestige = Club(state.CurrentClub)?.Prestige ?? 50;
        var opponents = state.WorldClubs.Where(x => x.Club != state.CurrentClub)
            .OrderBy(x => Math.Abs(x.SquadStrength - ownPrestige) + NextInt(state, 18)).Take(8).ToList();
        var rival = opponents.Count == 0 ? LeagueTeams(state).First(x => x != state.CurrentClub) : opponents[NextInt(state, opponents.Count)].Club;
        var home = Next(state) >= .5;
        var fixture = new MatchFixture { Matchday = state.CurrentMatchday, Competition = campaign.Competition, Home = home ? state.CurrentClub : rival, Away = home ? rival : state.CurrentClub };
        state.Fixtures.Add(fixture);
        var matchEvent = CreateMatchEvent(state, fixture);
        matchEvent.Id = $"tournament-{campaign.Id}-{fixture.Id}";
        matchEvent.Title = $"{campaign.Competition}: noche decisiva";
        matchEvent.Category = campaign.Type;
        matchEvent.Description = $"{campaign.Format}. {campaign.Competition} no se resuelve en automático: tu acción puede inclinar esta eliminatoria ante {rival}.";
        matchEvent.Match!.Stakes = campaign.Type == "Continental"
            ? "Una buena actuación eleva tu prestigio internacional y el interés de clubes extranjeros."
            : "La copa nacional ofrece un título real y un camino alternativo hacia Europa o el continente.";
        matchEvent.Match.IsDecisive = true;
        state.Timeline.Add($"{campaign.Competition}: momento clave ante {rival}.");
        return matchEvent;
    }

    private void ResolveTournamentKeyMoment(CareerState state, MatchFixture fixture, bool actionSuccess)
    {
        var campaign = state.TournamentCampaigns.FirstOrDefault(x => x.Competition == fixture.Competition);
        if (campaign is null || campaign.KeyMomentPlayed) return;
        campaign.KeyMomentPlayed = true;
        campaign.Played++;
        var home = fixture.Home == state.CurrentClub;
        var ownGoals = home ? fixture.HomeGoals ?? 0 : fixture.AwayGoals ?? 0;
        var rivalGoals = home ? fixture.AwayGoals ?? 0 : fixture.HomeGoals ?? 0;
        campaign.Goals += ownGoals;
        var progressed = ownGoals > rivalGoals || (ownGoals == rivalGoals && actionSuccess);
        if (progressed)
        {
            campaign.Wins++;
            campaign.Phase = campaign.Type == "Continental" ? "Clasificado a eliminatorias" : "Octavos de final";
            campaign.BestRound = campaign.Phase;
            campaign.KeyMatches.Add($"{state.CurrentClub} {ownGoals}-{rivalGoals} {(fixture.Home == state.CurrentClub ? fixture.Away : fixture.Home)} · avanzaste");
            state.Player.Reputation = Cap(state.Player.Reputation + (campaign.Type == "Continental" ? 3 : 2));
        }
        else
        {
            campaign.Phase = "Eliminado";
            campaign.BestRound = "Eliminado en momento clave";
            campaign.KeyMatches.Add($"{state.CurrentClub} {ownGoals}-{rivalGoals} {(fixture.Home == state.CurrentClub ? fixture.Away : fixture.Home)} · eliminado");
        }
    }

    private static TournamentCampaign CloneCampaign(TournamentCampaign source) => new()
    {
        Id = source.Id, Season = source.Season, Competition = source.Competition, Type = source.Type, Format = source.Format,
        Phase = source.Phase, Qualified = source.Qualified, KeyMomentPlayed = source.KeyMomentPlayed, Played = source.Played,
        Wins = source.Wins, Goals = source.Goals, BestRound = source.BestRound, Champion = source.Champion,
        RunnerUp = source.RunnerUp, PrizeEur = source.PrizeEur, KeyMatches = source.KeyMatches.ToList()
    };

    private void SimulateInternationalFinals(CareerState state)
    {
        var p = state.Player; var team = state.NationalTeam;
        if (team is null || p.Age < 17 || state.NationalHistory.Any(x => x.Season == state.Season && x.Competition.Contains("Copa"))) return;
        var competition = state.Season % 4 == 2 ? "Copa del Mundo · 12 grupos y dieciseisavos" : team.Confederation switch
        {
            "CONCACAF" => "Copa Oro · grupos y eliminatorias",
            "CONMEBOL" => "Copa América · grupos y eliminatorias",
            "UEFA" => state.Season % 2 == 0 ? "Eurocopa · grupos y eliminatorias" : "UEFA Nations League",
            "AFC" => "Copa Asia · 24 selecciones",
            "CAF" => "Copa Africana de Naciones · 24 selecciones",
            _ => "Torneo internacional"
        };
        var chance = Math.Clamp(.12 + p.Reputation / 180d + Math.Max(0, p.Overall - team.Strength + 10) / 110d + (p.LastSeasonAverage >= 6.8 ? .15 : 0), .12, .86);
        if (Next(state) > chance) return;
        var appearances = 2 + NextInt(state, 6); var deepRun = Next(state) < Math.Clamp(team.Strength / 115d + p.Overall / 260d, .28, .88);
        var goals = Enumerable.Range(0, appearances).Count(_ => Next(state) < (p.Position is "Delantero" or "Extremo" ? .28 : p.Position == "Mediocampista" ? .13 : .03));
        var outcome = deepRun ? "semifinal o final" : "fase de grupos u octavos";
        p.NationalAppearances += appearances; p.NationalGoals += goals; p.Reputation = Cap(p.Reputation + (deepRun ? 7 : 3)); p.Morale = Cap(p.Morale + (deepRun ? 6 : 2)); p.Energy = Floor(p.Energy - appearances * 2);
        state.NationalHistory.Add(new NationalCampaign { Season = state.Season, Country = team.Country, Competition = competition, Appearances = appearances, Goals = goals, Outcome = outcome });
        state.CareerMilestones.Add($"{state.Season}: {team.Country} te lleva a {competition}; {appearances} PJ, {goals} goles, {outcome}.");
    }

    private List<MatchFixture> GenerateFixtures(CareerState state)
    {
        var leagueName = state.CurrentLeague;
        var teams = LeagueTeams(state);
        if (teams.Count % 2 != 0) teams.Add("__BYE__");
        var rotation = teams.ToList(); var fixtures = new List<MatchFixture>(); var rounds = rotation.Count - 1;
        for (var round = 0; round < rounds; round++)
        {
            for (var i = 0; i < rotation.Count / 2; i++)
            {
                var home = rotation[i]; var away = rotation[rotation.Count - 1 - i];
                if (home != "__BYE__" && away != "__BYE__") fixtures.Add(new MatchFixture { Matchday = round + 1, Competition = leagueName, Home = round % 2 == 0 ? home : away, Away = round % 2 == 0 ? away : home });
            }
            rotation = [rotation[0], rotation[^1], .. rotation.Skip(1).Take(rotation.Count - 2)];
        }
        var league = _world.Leagues.First(l => l.Name == leagueName);
        if (league.MatchesPerTeam > rounds)
        {
            var secondLeg = fixtures.Select(f => new MatchFixture { Matchday = f.Matchday + rounds, Competition = leagueName, Home = f.Away, Away = f.Home }).ToList();
            fixtures.AddRange(secondLeg);
        }
        return fixtures;
    }

    private bool ShouldCreateMatchEvent(CareerState state, MatchFixture fixture)
    {
        if (state.EventIndex >= state.SeasonEventTarget) return false;
        var rival = fixture.Home == state.CurrentClub ? fixture.Away : fixture.Home;
        var rivalPrestige = Club(rival)?.Prestige ?? 50; var own = Club(state.CurrentClub)?.Prestige ?? 50;
        return state.ImportantMatchdays.Contains(state.CurrentMatchday) && state.EventIndex < state.SeasonEventTarget;
    }

    private SeasonEvent CreateMatchEvent(CareerState state, MatchFixture fixture)
    {
        var home = fixture.Home == state.CurrentClub; var rival = home ? fixture.Away : fixture.Home;
        var teamGoals = NextInt(state, 2); var rivalGoals = NextInt(state, 2); var minute = 64 + NextInt(state, 23);
        var decisive = state.CurrentMatchday == state.ImportantMatchdays.Max();
        var narrative = EventTemplate(state, "match");
        var stakes = decisive ? "Partido decisivo: este resultado puede definir título, clasificación o descenso." : StateOfMatch(state, rival);
        var isDefender = state.Player.Position is "Portero" or "Defensa";
        if (decisive) { teamGoals = isDefender ? 1 : 0; rivalGoals = isDefender ? 1 : 0; }
        var options = CreateMatchActions(state);
        return new SeasonEvent
        {
            Id = $"match-{fixture.Id}", Category = "Partido", MiniGame = decisive ? (isDefender ? "Última intervención" : "Definición decisiva") : (isDefender ? "Anticipación" : "Decisión ofensiva"),
            TemplateId = narrative.Id, Title = $"{state.CurrentClub} vs {rival} · {narrative.Title}", Description = $"Jornada {state.CurrentMatchday}, minuto {minute}. {narrative.Description} {stakes}",
            Match = new MatchContext { FixtureId = fixture.Id, Matchday = state.CurrentMatchday, Rival = rival, IsHome = home, Minute = minute, TeamGoals = teamGoals, RivalGoals = rivalGoals, Stakes = stakes, IsDecisive = decisive },
            Options = options,
            Challenge = decisive && !state.SeasonMiniGameUsed && IsMatchMiniGame(state.SeasonMiniGameId) ? CreateChallenge(state, state.SeasonMiniGameId) : null
        };
    }

    private List<EventOption> CreateMatchActions(CareerState state)
    {
        EventOption Action(string id, string label, string risk, string type, bool concede, int bonus, params string[] attributes) => new()
        {
            Id = id, Label = label, Risk = risk, ActionType = type, CanConcedeOnFailure = concede, SuccessBonus = bonus,
            AttributeWeights = attributes.ToList(), SuccessOutcome = type, FailureOutcome = concede ? "El rival puede castigar la pérdida" : "La jugada se diluye",
            Preview = [new() { Label = "Forma", Value = risk == "Bajo" ? 3 : 6, Direction = "positive" }, new() { Label = "Fama", Value = type is "goal" or "save" ? 5 : 3, Direction = "positive" }, new() { Label = "Moral", Value = 4, Direction = "positive" }]
        };
        List<List<EventOption>> groups = state.Player.Position switch
        {
            "Portero" => [
                [Action("gk-low", "Achicar y cubrir abajo", "Medio", "save", true, 3, "Goalkeeping", "Physical"), Action("gk-wait", "Aguantar hasta el último toque", "Alto", "save", true, 5, "Goalkeeping", "Pace"), Action("gk-angle", "Cerrar el primer palo", "Bajo", "save", false, 1, "Goalkeeping", "Positioning")],
                [Action("gk-cross", "Salir a blocar el centro", "Alto", "save", true, 4, "Goalkeeping", "Physical"), Action("gk-punch", "Despejar con los puños", "Medio", "clear", false, 2, "Goalkeeping", "Physical"), Action("gk-line", "Mantener la línea y reaccionar", "Bajo", "save", true, 0, "Goalkeeping", "Pace")],
                [Action("gk-penalty", "Lanzarte a tu lado fuerte", "Medio", "save", true, 3, "Goalkeeping", "Pace"), Action("gk-read", "Esperar la cadera del cobrador", "Alto", "save", true, 5, "Goalkeeping", "Goalkeeping"), Action("gk-psych", "Provocar al tirador desde la línea", "Bajo", "save", false, 1, "Goalkeeping", "Physical")]
            ],
            "Defensa" => [
                [Action("def-tackle", "Entrar fuerte al duelo", "Alto", "recovery", true, 4, "Defending", "Physical"), Action("def-jockey", "Perfilarlo hacia la banda", "Bajo", "clear", false, 2, "Defending", "Pace"), Action("def-cover", "Esperar apoyo y cerrar el pase", "Medio", "recovery", true, 2, "Defending", "Passing")],
                [Action("def-header", "Atacar el balón de cabeza", "Medio", "clear", true, 3, "Defending", "Physical"), Action("def-mark", "Bloquear a su rematador", "Bajo", "clear", false, 1, "Defending", "Pace"), Action("def-counter", "Anticipar y lanzar el contraataque", "Alto", "assist", true, 4, "Defending", "Passing")],
                [Action("def-line", "Romper la línea para interceptar", "Alto", "recovery", true, 5, "Defending", "Pace"), Action("def-clear", "Despejar sin complicarte", "Bajo", "clear", false, 1, "Defending", "Physical"), Action("def-play", "Salir jugando bajo presión", "Medio", "assist", true, 3, "Passing", "Dribbling")]
            ],
            "Mediocampista" => [
                [Action("mid-through", "Filtrar el pase entre centrales", "Alto", "assist", true, 5, "Passing", "Dribbling"), Action("mid-switch", "Cambiar de orientación", "Bajo", "chance", false, 2, "Passing", "Physical"), Action("mid-drive", "Conducir y atraer marcas", "Medio", "chance", true, 3, "Dribbling", "Pace"), Action("mid-shot", "Probar desde media distancia", "Alto", "goal", true, 4, "Shooting", "Physical")],
                [Action("mid-press", "Saltar a la presión", "Medio", "recovery", true, 4, "Defending", "Physical"), Action("mid-screen", "Cerrar la línea de pase", "Bajo", "clear", false, 2, "Defending", "Passing"), Action("mid-foul", "Cortar la contra con falta táctica", "Medio", "clear", false, 1, "Defending", "Physical")],
                [Action("mid-free", "Roscar el tiro libre al segundo palo", "Alto", "goal", true, 5, "Shooting", "Passing"), Action("mid-set", "Colgarla al punto de penal", "Medio", "assist", false, 3, "Passing", "Dribbling"), Action("mid-short", "Jugar corto y conservar", "Bajo", "chance", false, 1, "Passing", "Dribbling")]
            ],
            _ => [
                [Action("att-finish", "Cruzar el remate al palo largo", "Medio", "goal", true, 4, "Shooting", "Dribbling"), Action("att-chip", "Picarla sobre el portero", "Alto", "goal", true, 6, "Shooting", "Pace"), Action("att-square", "Cederla al compañero libre", "Bajo", "assist", false, 2, "Passing", "Dribbling"), Action("att-draw", "Amagar y forzar el penal", "Alto", "chance", true, 4, "Dribbling", "Pace")],
                [Action("att-header", "Atacar el primer palo", "Alto", "goal", true, 5, "Physical", "Shooting"), Action("att-backpost", "Llegar al segundo palo", "Medio", "goal", true, 3, "Pace", "Shooting"), Action("att-volley", "Bajarla para un remate limpio", "Medio", "chance", false, 2, "Dribbling", "Shooting")],
                [Action("att-free", "Buscar la escuadra en el tiro libre", "Alto", "goal", true, 6, "Shooting", "Passing"), Action("att-power", "Pegarle fuerte por encima de la barrera", "Medio", "goal", true, 3, "Shooting", "Physical"), Action("att-cross", "Mandar centro tenso al área", "Bajo", "assist", false, 2, "Passing", "Pace")],
                [Action("att-counter", "Encara al último defensor", "Alto", "goal", true, 5, "Dribbling", "Pace"), Action("att-cut", "Recorta y dispara", "Medio", "goal", true, 4, "Dribbling", "Shooting"), Action("att-combine", "Pared rápida para romper líneas", "Bajo", "assist", false, 2, "Passing", "Dribbling")]
            ]
        };
        var eligible = groups.Where(g => !state.RecentMatchSituations.Contains(g[0].Id.Split('-')[0] + g[0].Id.Split('-')[1])).ToList();
        var picked = (eligible.Count > 0 ? eligible : groups)[NextInt(state, eligible.Count > 0 ? eligible.Count : groups.Count)];
        var key = picked[0].Id.Split('-')[0] + picked[0].Id.Split('-')[1];
        state.RecentMatchSituations.Add(key);
        if (state.RecentMatchSituations.Count > 5) state.RecentMatchSituations.RemoveAt(0);
        return picked;
    }

    private double MatchActionSuccessChance(CareerState state, SeasonEvent active, EventOption option)
    {
        var p = state.Player;
        var attribute = option.AttributeWeights.Count == 0 ? p.Overall : option.AttributeWeights.Average(name => name switch
        {
            "Pace" => p.Pace, "Shooting" => p.Shooting, "Passing" => p.Passing, "Dribbling" => p.Dribbling,
            "Defending" => p.Defending, "Physical" => p.Physical, "Goalkeeping" or "Positioning" => p.Goalkeeping, _ => p.Overall
        });
        var rivalStrength = Club(active.Match!.Rival)?.Prestige ?? 50;
        var pressure = active.Match.IsDecisive ? .04 : 0;
        return Math.Clamp(.13 + p.Overall / 250d + attribute / 190d + p.Form / 420d + p.Energy / 650d + p.Morale / 900d + option.SuccessBonus / 100d - rivalStrength / 420d - RiskPenalty(option.Risk) - pressure, .10, .89);
    }

    private void ResolveBackgroundFixture(CareerState state, MatchFixture fixture)
    {
        var homeStrength = (Club(fixture.Home)?.Prestige ?? 50) + 5; var awayStrength = Club(fixture.Away)?.Prestige ?? 50;
        fixture.HomeGoals = Goals(state, homeStrength, awayStrength); fixture.AwayGoals = Goals(state, awayStrength, homeStrength); fixture.IsPlayed = true;
    }

    private void ResolvePlayerFixture(CareerState state, MatchFixture fixture, SeasonEvent? active, bool success, EventOption? action = null)
    {
        var p = state.Player; var home = fixture.Home == state.CurrentClub;
        UpdateClubRole(state);
        var ownStrength = (Club(state.CurrentClub)?.Prestige ?? 50) + p.Overall / 7 + (home ? 5 : 0);
        var rival = home ? fixture.Away : fixture.Home; var rivalStrength = Club(rival)?.Prestige ?? 50;
        var ownGoals = active?.Match?.TeamGoals ?? Goals(state, ownStrength, rivalStrength);
        var rivalGoals = active?.Match?.RivalGoals ?? Goals(state, rivalStrength, ownStrength);
        if (active is null && Next(state) > AppearanceChance(state.ClubRole))
        {
            fixture.HomeGoals = home ? ownGoals : rivalGoals; fixture.AwayGoals = home ? rivalGoals : ownGoals; fixture.IsPlayed = true;
            p.Morale = Math.Max(25, p.Morale - 1); state.Timeline.Add($"Jornada {state.CurrentMatchday}: no sumaste minutos ({state.ClubRole}).");
            return;
        }
        if (active is not null)
        {
            var impact = action?.ActionType ?? "chance";
            if (success)
            {
                if (impact is "goal" or "assist") ownGoals++;
                p.Form = Math.Min(95, p.Form + (impact is "goal" or "save" ? 8 : 5));
                p.Reputation += impact is "goal" or "save" ? 5 : 3;
                p.Morale = Math.Min(95, p.Morale + 5);
            }
            else
            {
                if (action?.CanConcedeOnFailure ?? true) rivalGoals++;
                p.Form = Math.Max(30, p.Form - (action?.Risk == "Bajo" ? 2 : 5));
                p.Energy = Math.Max(25, p.Energy - 7);
                p.Morale = Math.Max(25, p.Morale - 3);
            }
        }
        fixture.HomeGoals = home ? ownGoals : rivalGoals; fixture.AwayGoals = home ? rivalGoals : ownGoals; fixture.IsPlayed = true;
        var normalGoal = false; var normalAssist = false;
        if (active is null)
        {
            var goalChance = p.Position switch { "Delantero" => .13, "Extremo" => .09, "Mediocampista" => .055, "Defensa" => .025, _ => .005 } * Math.Clamp((p.Shooting + p.Form) / 130d, .55, 1.45);
            var assistChance = p.Position switch { "Mediocampista" => .12, "Extremo" => .11, "Delantero" => .06, "Defensa" => .035, _ => .01 } * Math.Clamp((p.Passing + p.Form) / 130d, .55, 1.4);
            if (Next(state) < goalChance) { ownGoals++; p.Goals++; normalGoal = true; if (state.Contract is not null) AddLedger(state, "Prima", "Prima por gol", state.Contract.GoalOrAssistBonusEur); }
            else if (Next(state) < assistChance) { ownGoals++; p.Assists++; normalAssist = true; if (state.Contract is not null) AddLedger(state, "Prima", "Prima por asistencia", state.Contract.GoalOrAssistBonusEur); }
            fixture.HomeGoals = home ? ownGoals : rivalGoals; fixture.AwayGoals = home ? rivalGoals : ownGoals;
        }
        var rating = Math.Round(5.7 + (p.Overall - 50) / 17d + (success ? .9 : Next(state) - .55), 2);
        if (normalGoal) rating += .75; else if (normalAssist) rating += .4;
        p.Appearances++; p.RatingTotal += rating; p.Energy = Math.Max(25, p.Energy - 3); state.SeasonMinutes += state.ClubRole == "Titular" ? 82 : state.ClubRole == "Rotación" ? 58 : 28;
        if (success && action?.ActionType == "goal") { p.Goals++; if (state.Contract is not null) AddLedger(state, "Prima", "Prima por gol", state.Contract.GoalOrAssistBonusEur); }
        else if (success && action?.ActionType == "assist") { p.Assists++; if (state.Contract is not null) AddLedger(state, "Prima", "Prima por asistencia", state.Contract.GoalOrAssistBonusEur); }
        if (state.Contract is not null) AddLedger(state, "Prima", "Prima por aparición", state.Contract.AppearanceBonusEur);
    }

    private void UpdateClubRole(CareerState state)
    {
        var p = state.Player; var clubStrength = state.WorldClubs.FirstOrDefault(x => x.Club == state.CurrentClub)?.SquadStrength ?? Club(state.CurrentClub)?.Prestige ?? 55;
        var score = p.Overall + p.Form / 8d + p.CoachRelation / 12d + p.Morale / 18d - p.InjuryRisk / 8d - clubStrength;
        var next = score >= 8 ? "Titular" : score >= -2 ? "Rotación" : score >= -10 ? "Suplente" : "Fuera de convocatoria";
        if (state.ClubRole != next) { state.ClubRole = next; state.CareerMilestones.Add($"{state.Season}: tu rol cambia a {next} en {state.CurrentClub}."); state.Timeline.Add($"El técnico te considera {next}."); }
    }
    private static double AppearanceChance(string role) => role switch { "Titular" => .94, "Rotación" => .72, "Suplente" => .43, _ => .16 };
    private void RefreshObjectives(CareerState state)
    {
        var p = state.Player; var seasonGoals = p.Goals - state.SeasonStartGoals; var seasonAssists = p.Assists - state.SeasonStartAssists;
        state.Objectives = [state.ClubRole == "Titular" ? "El técnico confía en ti: protege tu puesto con regularidad." : "El técnico espera una señal para darte más minutos.", p.LastSeasonAverage >= 7.2 || (p.Appearances > state.SeasonStartAppearances && (p.RatingTotal - state.SeasonStartRatingTotal) / Math.Max(1, p.Appearances - state.SeasonStartAppearances) >= 7.2) ? "Tu nivel ya atrae miradas de un escalón superior." : "Una racha de buenas actuaciones puede abrir el mercado.", seasonGoals >= 8 || seasonAssists >= 6 ? "Tu producción ya compite por reconocimiento de liga." : "La producción ofensiva definirá si entras a premios de temporada."];
    }

    private SeasonEvent CreateOffPitchEvent(CareerState state, SeasonEvent matchEvent, bool matchSuccess)
    {
        var match = matchEvent.Match!;
        if (!state.NationalEventUsed && ShouldReceiveNationalCallup(state, matchSuccess)) return CreateNationalEvent(state, match);
        if (!state.WorldEventUsed && ShouldCreateWorldClubEvent(state, matchSuccess)) return CreateWorldClubEvent(state, match);
        if (!state.SeasonMiniGameUsed && !IsMatchMiniGame(state.SeasonMiniGameId))
        {
            var game = state.SeasonMiniGameId;
            var isCasino = game is "casino-dice" or "roulette" or "blackjack";
            var category = isCasino ? "Ocio" : "Entrenamiento";
            return new SeasonEvent
            {
                Id = $"minigame-{state.Season}-{game}", Category = category, Title = MiniGameName(game), MiniGame = MiniGameName(game),
                Description = isCasino ? "Ocio ficticio con dinero interno: elige tu jugada y acepta sus consecuencias." : "Una sesión breve antes de volver al calendario. Completa el desafío para obtener el beneficio.",
                Challenge = CreateChallenge(state, game), Options = [new EventOption { Id = "play", Label = "Resolver desafío", Risk = isCasino ? "Suerte" : "Habilidad" }]
            };
        }
        if (Next(state) < .48)
        {
            var trigger = Next(state) < .42 ? "life" : Next(state) < .74 ? "board" : Next(state) < .94 ? "finance" : "integrity";
            var template = EventTemplate(state, trigger);
            var category = trigger switch { "life" => "Vida personal", "board" => "Directiva", "finance" => "Finanzas", _ => "Integridad" };
            var choices = category == "Integridad"
                ? new[] { ("report", "Rechazar y denunciar la propuesta", "Bajo"), ("accept", "Aceptar el trato", "Extremo") }
                : category == "Finanzas"
                    ? new[] { ("safe", "Elegir la opción prudente", "Bajo"), ("risk", "Buscar mayor retorno", "Alto") }
                    : new[] { ("responsible", "Actuar con responsabilidad", "Bajo"), ("impulse", "Dejarte llevar por el momento", "Alto") };
            return OffPitch(template.Id, category, template.Title, string.IsNullOrWhiteSpace(template.Description) ? $"El rendimiento ante {match.Rival} abrió una situación fuera de cancha." : template.Description, template.MiniGame, choices);
        }
        if (state.Player.Energy < 58 || state.Player.InjuryRisk > 38)
        {
            var template = EventTemplate(state, "recovery");
            return OffPitch(template.Id, "Recuperación", template.Title, $"Después del partido ante {match.Rival}, el cuerpo técnico detectó carga alta. Esta decisión afectará tu disponibilidad para las próximas jornadas.", template.MiniGame,
                ("rest", "Bajar cargas y hacer recuperación", "Bajo"), ("push", "Entrenar pese a la molestia", "Alto"));
        }
        if (!matchSuccess)
        {
            var template = EventTemplate(state, "press");
            return OffPitch(template.Id, "Prensa", template.Title, $"El resultado ante {match.Rival} dejó preguntas sobre tu actuación. Tu respuesta afectará al técnico, la afición y los medios.", template.MiniGame,
                ("team", "Asumir responsabilidad y defender al grupo", "Bajo"), ("blame", "Cuestionar el planteamiento", "Alto"));
        }
        var training = EventTemplate(state, "training");
        return new SeasonEvent { Id = training.Id, TemplateId = training.Id, Category = "Entrenamiento", Title = training.Title, MiniGame = training.MiniGame,
            Description = $"Tu actuación ante {match.Rival} te dejó crédito con el entrenador. Elige una rutina: dos son favorables y una tiene riesgo de retroceso.", Options = CreateTrainingPlanOptions(state), RequiredSelections = 1 };
    }

    private EventTemplate EventTemplate(CareerState state, string trigger)
    {
        var eligible = _events.Templates.Where(template => template.Trigger == trigger && template.MinAge <= state.Player.Age && (trigger == "match" || !state.SeenEventIds.Contains(template.Id)) && (!state.FamilyLastSeason.TryGetValue(template.Family, out var last) || state.Season - last >= 3)).ToList();
        if (eligible.Count == 0) return new EventTemplate { Id = $"fallback-{trigger}-{state.Season}", Trigger = trigger, Category = "Recuperación", Family = "fallback", Title = "Situación imprevista", MiniGame = "Decisión" };
        var roll = Next(state);
        var rarity = roll < .68 ? "common" : roll < .93 ? "rare" : "superrare";
        var tier = eligible.Where(x => x.Rarity == rarity).ToList();
        var candidates = tier.Count > 0 ? tier : eligible;
        return candidates[NextInt(state, candidates.Count)];
    }

    private void RecordTemplate(CareerState state, string eventId, string? context = null)
    {
        var template = _events.Templates.FirstOrDefault(x => x.Id == eventId);
        var recordId = string.IsNullOrWhiteSpace(context) ? eventId : $"{eventId}:{context}";
        if (template is null || state.SeenEventIds.Contains(recordId)) return;
        state.SeenEventIds.Add(recordId);
        state.FamilyLastSeason[template.Family] = state.Season;
    }

    private static SeasonEvent OffPitch(string id, string category, string title, string description, string minigame, params (string Id, string Label, string Risk)[] options) => new()
    {
        Id = id, TemplateId = id, Category = category, Title = title, Description = description, MiniGame = minigame,
        Options = options.Select(x => new EventOption { Id = x.Id, Label = x.Label, Risk = x.Risk }).ToList()
    };

    private static SeasonEvent EventFromTemplate(EventTemplate template, string description)
    {
        var safeId = template.Category == "Finanzas" ? "safe" : template.Category == "Integridad" ? "report" : "responsible";
        var riskId = template.Category == "Finanzas" ? "risk" : template.Category == "Integridad" ? "accept" : "impulse";
        var risk = template.Category == "Integridad" ? "Extremo" : "Alto";
        var result = OffPitch(template.Id, template.Category, template.Title, description, template.MiniGame,
            (safeId, template.SafeOption, "Bajo"), (riskId, template.RiskOption, risk));
        result.TemplateId = template.Id;
        return result;
    }

    private bool ShouldReceiveNationalCallup(CareerState state, bool matchSuccess)
    {
        if (state.Player.Age < 17 || state.NationalTeam is null) return false;
        var threshold = state.NationalTeam.Strength + (state.NationalTeam.HistoricalTitles > 2 ? 8 : 0);
        var chance = .03 + state.Player.Reputation / 260d + Math.Max(0, state.Player.Overall - threshold + 10) / 180d + (matchSuccess ? .05 : 0);
        return Next(state) < Math.Clamp(chance, .04, .48);
    }

    private SeasonEvent CreateNationalEvent(CareerState state, MatchContext match)
    {
        var team = state.NationalTeam!;
        var competition = state.Season % 4 == 2 ? "Copa del Mundo" : state.Season % 2 == 0 ? "el torneo continental" : "la fecha FIFA";
        return OffPitch($"national-{state.Season}-{state.CurrentMatchday}", "Selección", $"Convocatoria de {team.Country}",
            $"Tu actuación ante {match.Rival} llamó la atención del cuerpo técnico. {team.Country} te considera para {competition}; aceptar puede darte prestigio, pero añade carga al calendario.", "Decisión de selección",
            ("accept", "Aceptar la convocatoria", "Carga media"), ("decline", "Priorizar al club y rechazar", "Reputación nacional"));
    }

    private CareerState ResolveNationalEvent(CareerState state, SeasonEvent active, EventOption option)
    {
        var p = state.Player; var team = state.NationalTeam!; state.NationalEventUsed = true;
        if (option.Id == "decline")
        {
            p.CoachRelation = Floor(p.CoachRelation - 2); p.MediaRelation = Floor(p.MediaRelation - 5); p.Morale = Floor(p.Morale - 2);
            active.Outcome = $"Rechazaste a {team.Country} para priorizar {state.CurrentClub}. La federación y los medios cuestionaron la decisión.";
            active.Resolution = new EventResolution { Headline = active.Title, Result = "Convocatoria rechazada", Detail = active.Outcome, Effects = [new() { Label = "Medios", Value = -5, Direction = "negative" }, new() { Label = "Energía", Value = 0, Direction = "neutral" }] };
        }
        else
        {
            var appearances = 1 + NextInt(state, 4);
            var goalChance = p.Position is "Delantero" or "Extremo" ? .38 : p.Position == "Mediocampista" ? .18 : .04;
            var goals = Enumerable.Range(0, appearances).Count(_ => Next(state) < goalChance);
            var deepRun = Next(state) < Math.Clamp(team.Strength / 130d + p.Reputation / 350d, .25, .82);
            var competition = state.Season % 4 == 2 ? "Copa del Mundo" : state.Season % 2 == 0 ? "torneo continental" : "amistosos y eliminatorias";
            p.NationalAppearances += appearances; p.NationalGoals += goals; p.Reputation += deepRun ? 6 : 3; p.FansRelation = Cap(p.FansRelation + (deepRun ? 5 : 2)); p.Energy = Floor(p.Energy - (5 + appearances * 2)); p.Morale = Cap(p.Morale + (deepRun ? 5 : 2));
            var outcome = deepRun ? "clasificación y protagonismo" : "participación con aprendizaje";
            state.NationalHistory.Add(new NationalCampaign { Season = state.Season, Country = team.Country, Competition = competition, Appearances = appearances, Goals = goals, Outcome = outcome });
            active.Outcome = $"Debutaste o sumaste minutos con {team.Country}: {appearances} PJ, {goals} goles y {outcome} en {competition}.";
            active.Resolution = new EventResolution { Headline = active.Title, Result = "Convocatoria aprovechada", Detail = active.Outcome, Effects = [new() { Label = "Reputación", Value = deepRun ? 6 : 3, Direction = "positive" }, new() { Label = "Energía", Value = -(5 + appearances * 2), Direction = "negative" }, new() { Label = "Partidos selección", Value = appearances, Direction = "positive" }] };
        }
        RecalculateOverall(p); state.LastResolution = active.Resolution; state.CompletedEvents.Add(active); state.Timeline.Add(active.Outcome); state.ActiveEvent = null;
        return state;
    }

    public CareerState UseActivity(CareerState state, string activityId)
    {
        EnsureLifestyle(state);
        if (state.IsRetired) throw new InvalidOperationException("La carrera ya terminó.");
        var key = activityId.Split(':')[0];
        if (state.ActivityCooldowns.TryGetValue(key, out var availableAt) && availableAt > state.CurrentMatchday)
            throw new InvalidOperationException($"Esta actividad estará disponible después de la jornada {availableAt}.");
        var p = state.Player;
        if (activityId.StartsWith("use:")) return UseConsumable(state, activityId[4..]);
        if (activityId == "casino")
        {
            if (p.Age < 18) throw new InvalidOperationException("El casino ficticio solo está disponible para mayores de edad.");
            state.ActivityCooldowns[key] = state.CurrentMatchday + 2;
            var game = SeasonMiniGames[12 + NextInt(state, 3)];
            state.ActiveEvent = new SeasonEvent
            {
                Id = $"hub-casino-{state.Season}-{state.CurrentMatchday}", Category = "Ocio", Title = "Casino ficticio",
                Description = "Entras con saldo estrictamente interno a la partida. Elige tu jugada: el riesgo y el resultado quedarán registrados.",
                MiniGame = game, Challenge = CreateChallenge(state, game), IsHubActivity = true,
                Options = [new EventOption { Id = "play", Label = "Jugar esta mano", Risk = "Suerte" }]
            };
            return state;
        }
        state.ActivityCooldowns[key] = state.CurrentMatchday + 2;
        switch (activityId)
        {
            case "shop:recovery":
                Spend(state, 350m, "Tienda", "Kit de recuperación"); p.Energy = Cap(p.Energy + 14); p.InjuryRisk = Floor(p.InjuryRisk - 8);
                return CompleteActivity(state, "Kit de recuperación", "Compraste un kit de recuperación y llegas con mejores sensaciones al siguiente bloque.", [new() { Label = "Energía", Value = 14, Direction = "positive" }, new() { Label = "Riesgo lesión", Value = -8, Direction = "positive" }]);
            case "shop:boots":
                Spend(state, 900m, "Tienda", "Botas personalizadas"); AdjustAttribute(p, p.Position is "Defensa" ? "defending" : p.Position == "Portero" ? "goalkeeping" : "dribbling", 2); RecalculateOverall(p);
                return CompleteActivity(state, "Botas personalizadas", "El material personalizado mejora tu confianza técnica y se adapta a tu posición.", [new() { Label = "Atributo clave", Value = 2, Direction = "positive" }]);
            case "training":
                Spend(state, 500m, "Preparación", "Sesión individual"); var focus = DevelopmentAttributes(p).First(); AdjustAttribute(p, focus, 2); p.Energy = Floor(p.Energy - 6); p.Morale = Cap(p.Morale + 2); RecalculateOverall(p);
                return CompleteActivity(state, "Sesión individual", $"Trabajaste {PreseasonLabel(focus)} con un especialista. El progreso llega a costa de energía.", [new() { Label = PreseasonLabel(focus), Value = 2, Direction = "positive" }, new() { Label = "Energía", Value = -6, Direction = "negative" }]);
            case "recovery":
                p.Energy = Cap(p.Energy + 10); p.InjuryRisk = Floor(p.InjuryRisk - 6); p.Form = Floor(p.Form - 1);
                return CompleteActivity(state, "Recuperación", "Priorizaste dormir, movilidad y fisioterapia; sacrificas un poco de ritmo para llegar sano.", [new() { Label = "Energía", Value = 10, Direction = "positive" }, new() { Label = "Riesgo lesión", Value = -6, Direction = "positive" }]);
            case "agent":
                Spend(state, 450m, "Agente", "Informe de mercado"); p.Reputation = Cap(p.Reputation + 2); p.MediaRelation = Cap(p.MediaRelation + 2); RefreshMarketProfile(state);
                var scout = state.MarketProfile.ScoutingClubs.FirstOrDefault() ?? "clubes de tu escalón";
                return CompleteActivity(state, "Reunión con agente", $"Tu agente actualizó tu dossier. {scout} aparece en el radar si mantienes el rendimiento.", [new() { Label = "Fama", Value = 2, Direction = "positive" }, new() { Label = "Mercado", Value = 1, Direction = "positive" }]);
            case "social":
                Spend(state, 180m, "Vida personal", "Plan social responsable"); p.Morale = Cap(p.Morale + 7); p.FansRelation = Cap(p.FansRelation + 2); p.Energy = Floor(p.Energy - 2);
                return CompleteActivity(state, "Vida personal", "Desconectaste con responsabilidad y recuperaste confianza fuera del campo.", [new() { Label = "Moral", Value = 7, Direction = "positive" }, new() { Label = "Energía", Value = -2, Direction = "negative" }]);
            case "nutrition":
                Spend(state, ServiceCost(state, .018m), "Servicios", "Bloque de nutrición"); p.Energy = Cap(p.Energy + 6); state.Lifestyle.Wellbeing = Cap(state.Lifestyle.Wellbeing + 4);
                return CompleteActivity(state, "Nutrición deportiva", "Un plan de alimentación mejora tu recuperación sin sustituir el entrenamiento.", [new() { Label = "Energía", Value = 6, Direction = "positive" }]);
            case "psychology":
                Spend(state, ServiceCost(state, .016m), "Servicios", "Psicología deportiva"); p.Morale = Cap(p.Morale + 8); p.MediaRelation = Cap(p.MediaRelation + 2);
                return CompleteActivity(state, "Psicología deportiva", "Trabajaste foco y presión competitiva.", [new() { Label = "Moral", Value = 8, Direction = "positive" }]);
            case "media":
                Spend(state, ServiceCost(state, .022m), "Servicios", "Media training"); p.MediaRelation = Cap(p.MediaRelation + 7); p.Reputation = Cap(p.Reputation + 2); state.Lifestyle.PublicImage = Cap(state.Lifestyle.PublicImage + 5);
                return CompleteActivity(state, "Media training", "Preparaste mensajes y entrevistas con un equipo profesional.", [new() { Label = "Medios", Value = 7, Direction = "positive" }, new() { Label = "Fama", Value = 2, Direction = "positive" }]);
            default: throw new InvalidOperationException("La actividad no existe.");
        }
    }

    public List<ShopProductQuote> ShopCatalog(CareerState state)
    {
        EnsureLifestyle(state);
        var salary = state.Contract?.AnnualGrossEur ?? 30_000m;
        var products = new (string Id, string Name, string Family, string Benefit, decimal Price, decimal Maintenance, bool Permanent)[]
        {
            ("recovery-kit","Kit de recuperación","Consumible","+14 energía · −8 riesgo",Math.Max(180m,salary*.004m),0,false),("sports-meal","Comida deportiva","Consumible","+7 energía",Math.Max(90m,salary*.002m),0,false),("sleep-guide","Sueño guiado","Consumible","+10 energía · −3 riesgo",Math.Max(110m,salary*.0025m),0,false),("preventive-wrap","Vendaje preventivo","Consumible","−9 riesgo",Math.Max(75m,salary*.0015m),0,false),("mobility","Sesión de movilidad","Consumible","+5 energía · −5 riesgo",Math.Max(130m,salary*.003m),0,false),
            ("boots","Botas por perfil","Equipo","Mejora técnica pequeña",Math.Max(700m,salary*.016m),0,true),("gps","GPS de carga","Equipo","Prevención de lesión",Math.Max(900m,salary*.02m),0,true),("home-gym","Gimnasio doméstico","Equipo","+5 bienestar",Math.Max(2200m,salary*.05m),0,true),("video","Análisis de vídeo","Equipo","+3 relación técnico",Math.Max(650m,salary*.014m),0,true),
            ("apartment","Vivienda cómoda","Lujo","Confort y moral",Math.Max(4000m,salary*.09m),Math.Max(500m,salary*.01m),true),("car","Auto personal","Lujo","Evento situacional",Math.Max(6500m,salary*.14m),Math.Max(700m,salary*.015m),true),("style","Estilo e imagen","Lujo","Imagen y fama",Math.Max(1300m,salary*.028m),0,true)
        };
        return products.Select(x => new ShopProductQuote { Id=x.Id, Name=x.Name, Family=x.Family, Benefit=x.Benefit, PriceEur=x.Price, MaintenanceEur=x.Maintenance, Inventory=state.Lifestyle.Inventory.GetValueOrDefault(x.Id), Owned=state.Lifestyle.PermanentPurchases.Contains(x.Id), CanBuy=state.Player.Money >= x.Price && (!x.Permanent || !state.Lifestyle.PermanentPurchases.Contains(x.Id)) && (x.Permanent || state.Lifestyle.Inventory.GetValueOrDefault(x.Id) < 3) }).ToList();
    }

    public CasinoQuote CasinoQuote(CareerState state, string game, int betPercent)
    {
        EnsureLifestyle(state); var valid = game is "dice" or "roulette" or "blackjack" && betPercent is 1 or 2 or 4;
        var bet = valid ? Math.Floor(state.Player.Money * betPercent / 100m) : 0m;
        var reason = !valid ? "Selección inválida" : state.Player.Age < 18 ? "Disponible a los 18 años" : state.CasinoSession is not null ? "Ya tienes una sesión abierta" : state.ActivityCooldowns.GetValueOrDefault("casino") > state.CurrentMatchday ? $"Disponible después de jornada {state.ActivityCooldowns["casino"]}" : bet < 10m ? "Saldo mínimo €1,000" : null;
        return new CasinoQuote { Game=game, BetPercent=betPercent, BetEur=bet, OpeningBalanceEur=state.Player.Money, SessionLossLimitEur=Math.Min(5000m,state.Player.Money*.10m), Payout=game == "roulette" ? "Rojo/negro: +1× · verde: +14×" : game == "dice" ? "Acierto: +1×" : "Victoria: +1× · empate: €0", CanOpen=reason is null, BlockReason=reason };
    }

    public CareerState BuyProduct(CareerState state, string productId)
    {
        EnsureLifestyle(state);
        var p = state.Player; var salary = state.Contract?.AnnualGrossEur ?? 30_000m;
        var products = new Dictionary<string, (string name, string family, decimal price, bool permanent, decimal upkeep)>
        {
            ["recovery-kit"]=("Kit de recuperación","Consumible",Math.Max(180m,salary*.004m),false,0), ["sports-meal"]=("Comida deportiva","Consumible",Math.Max(90m,salary*.002m),false,0), ["sleep-guide"]=("Sueño guiado","Consumible",Math.Max(110m,salary*.0025m),false,0), ["preventive-wrap"]=("Vendaje preventivo","Consumible",Math.Max(75m,salary*.0015m),false,0), ["mobility"]=("Sesión de movilidad","Consumible",Math.Max(130m,salary*.003m),false,0),
            ["boots"]=("Botas por perfil","Equipo",Math.Max(700m,salary*.016m),true,0), ["gps"]=("GPS de carga","Equipo",Math.Max(900m,salary*.02m),true,0), ["home-gym"]=("Gimnasio doméstico","Equipo",Math.Max(2200m,salary*.05m),true,0), ["video"]=("Análisis de vídeo","Equipo",Math.Max(650m,salary*.014m),true,0),
            ["apartment"]=("Vivienda cómoda","Lujo",Math.Max(4000m,salary*.09m),true,Math.Max(500m,salary*.01m)), ["car"]=("Auto personal","Lujo",Math.Max(6500m,salary*.14m),true,Math.Max(700m,salary*.015m)), ["style"]=("Estilo e imagen","Lujo",Math.Max(1300m,salary*.028m),true,0)
        };
        if (!products.TryGetValue(productId, out var product)) throw new InvalidOperationException("Ese producto no existe.");
        if (product.permanent && state.Lifestyle.PermanentPurchases.Contains(productId)) throw new InvalidOperationException("Ya tienes esta compra permanente.");
        if (!product.permanent && state.Lifestyle.Inventory.GetValueOrDefault(productId) >= 3) throw new InvalidOperationException("Límite de tres unidades por consumible.");
        Spend(state, product.price, "Tienda · " + product.family, product.name);
        if (product.permanent) { state.Lifestyle.PermanentPurchases.Add(productId); state.Lifestyle.RecurringCostPerSeason += product.upkeep; ApplyPurchaseEffect(state, productId); }
        else state.Lifestyle.Inventory[productId] = state.Lifestyle.Inventory.GetValueOrDefault(productId) + 1;
        LogLifestyle(state, product.name, product.price, product.permanent ? "Compra permanente" : "Añadido al inventario");
        return CompleteActivity(state, product.name, product.permanent ? "Compra registrada: sus beneficios son situacionales y el mantenimiento se cobrará al inicio de la próxima temporada." : "Consumible guardado. Caduca al finalizar la temporada.", [new() { Label = "Saldo", Value = -(int)product.price, Direction = "negative" }]);
    }

    public CareerState OpenCasino(CareerState state, string game, int betPercent)
    {
        EnsureLifestyle(state); if (state.Player.Age < 18) throw new InvalidOperationException("El casino ficticio es solo para mayores de 18 años.");
        if (state.CasinoSession is not null) throw new InvalidOperationException("Ya hay una sesión de casino activa.");
        if (game is not ("dice" or "roulette" or "blackjack") || betPercent is not (1 or 2 or 4)) throw new InvalidOperationException("Elige un juego y apuesta predefinida válida.");
        if (state.ActivityCooldowns.GetValueOrDefault("casino") > state.CurrentMatchday) throw new InvalidOperationException("El casino está en enfriamiento durante dos jornadas.");
        var bet = Math.Floor(state.Player.Money * betPercent / 100m); if (bet < 10m) throw new InvalidOperationException("Necesitas al menos €1,000 de saldo para esta mesa.");
        state.CasinoSession = new CasinoSession { Game = game, OpeningBalance = state.Player.Money, LossLimit = Math.Min(5000m, state.Player.Money * .10m), BetEur = bet };
        return state;
    }

    public CareerState CasinoAction(CareerState state, string action)
    {
        EnsureLifestyle(state); var s = state.CasinoSession ?? throw new InvalidOperationException("No hay una sesión de casino activa.");
        if (action == "exit") { CloseCasino(state); return state; }
        if (s.HandsPlayed >= 5 || -s.NetResult >= s.LossLimit) throw new InvalidOperationException("La sesión alcanzó su límite; sal de la mesa.");
        if (s.Game == "blackjack") return Blackjack(state, action);
        if (action is not ("high" or "low" or "red" or "black" or "green")) throw new InvalidOperationException("La elección no pertenece a esta mesa.");
        var roll = s.Game == "dice" ? 1 + NextInt(state, 6) : NextInt(state, 37); var win = s.Game == "dice" ? (action == "high" ? roll >= 4 : roll <= 3) : action == "green" ? roll == 0 : action == "red" ? roll != 0 && (roll % 2 == 1) : roll != 0 && roll % 2 == 0;
        var multiplier = action == "green" ? 14m : 1m; FinishCasinoHand(state, win ? s.BetEur * multiplier : -s.BetEur, win ? "Victoria" : "Derrota", s.Game == "dice" ? $"Dado: {roll}. Elegiste {action}." : $"Ruleta: {roll}. Elegiste {action}."); return state;
    }

    private CareerState UseConsumable(CareerState state, string item)
    {
        if (state.Lifestyle.Inventory.GetValueOrDefault(item) < 1) throw new InvalidOperationException("No tienes ese consumible en el inventario.");
        var p = state.Player; state.Lifestyle.Inventory[item]--;
        var (energy, risk, morale) = item switch { "recovery-kit" => (14, -8, 0), "sports-meal" => (7, 0, 1), "sleep-guide" => (10, -3, 0), "preventive-wrap" => (0, -9, 0), "mobility" => (5, -5, 0), _ => throw new InvalidOperationException("Consumible inválido.") };
        p.Energy = Cap(p.Energy + energy); p.InjuryRisk = Floor(p.InjuryRisk + risk); p.Morale = Cap(p.Morale + morale); LogLifestyle(state, item, 0, "Consumido");
        return CompleteActivity(state, "Consumible usado", "Aplicaste un recurso de esta temporada; el efecto es moderado y no se acumula indefinidamente.", [new() { Label = "Energía", Value = energy, Direction = "positive" }, new() { Label = "Riesgo lesión", Value = risk, Direction = "positive" }]);
    }
    private static decimal ServiceCost(CareerState state, decimal ratio) => Math.Max(150m, (state.Contract?.AnnualGrossEur ?? 30_000m) * ratio);
    private static void EnsureLifestyle(CareerState state) { state.Lifestyle ??= new LifestyleProfile(); state.Lifestyle.Inventory ??= []; state.Lifestyle.PermanentPurchases ??= []; state.LifestyleHistory ??= []; state.CasinoHistory ??= []; state.ActivityCooldowns ??= []; }
    private static void LogLifestyle(CareerState state, string name, decimal cost, string result) => state.LifestyleHistory.Add(new LifestyleActivityRecord { Season = state.Season, Matchday = state.CurrentMatchday, Name = name, CostEur = cost, Result = result });
    private void ApplyPurchaseEffect(CareerState state, string id)
    {
        var p = state.Player;
        if (id == "boots") { AdjustAttribute(p, p.Position == "Portero" ? "goalkeeping" : "dribbling", 1); RecalculateOverall(p); }
        else if (id == "gps") p.InjuryRisk = Floor(p.InjuryRisk - 3);
        else if (id == "home-gym") state.Lifestyle.Wellbeing = Cap(state.Lifestyle.Wellbeing + 5);
        else if (id == "video") p.CoachRelation = Cap(p.CoachRelation + 3);
        else if (id == "apartment") { p.Morale = Cap(p.Morale + 4); state.Lifestyle.Wellbeing = Cap(state.Lifestyle.Wellbeing + 5); }
        else if (id == "style") { p.Reputation = Cap(p.Reputation + 2); state.Lifestyle.PublicImage = Cap(state.Lifestyle.PublicImage + 6); }
    }
    private CareerState Blackjack(CareerState state, string action)
    {
        var s = state.CasinoSession!;
        if (!s.HandInProgress) { if (action != "deal") throw new InvalidOperationException("Reparte la mano primero."); s.PlayerCards = [Card(state), Card(state)]; s.DealerCards = [Card(state), Card(state)]; s.HandInProgress = true; return state; }
        if (action == "hit") { s.PlayerCards.Add(Card(state)); if (HandValue(s.PlayerCards) > 21) FinishCasinoHand(state, -s.BetEur, "Exceso de 21", $"Tu mano sumó {HandValue(s.PlayerCards)}."); return state; }
        if (action == "double") { if (state.Player.Money < s.BetEur * 2) throw new InvalidOperationException("No hay saldo suficiente para doblar."); s.BetEur *= 2; s.PlayerCards.Add(Card(state)); return SettleBlackjack(state); }
        if (action == "stand") return SettleBlackjack(state);
        throw new InvalidOperationException("Acción de blackjack inválida.");
    }
    private CareerState SettleBlackjack(CareerState state) { var s = state.CasinoSession!; while (HandValue(s.DealerCards) < 17) s.DealerCards.Add(Card(state)); var you = HandValue(s.PlayerCards); var dealer = HandValue(s.DealerCards); var net = you > 21 ? -s.BetEur : dealer > 21 || you > dealer ? s.BetEur : you == dealer ? 0 : -s.BetEur; FinishCasinoHand(state, net, net > 0 ? "Victoria" : net == 0 ? "Empate" : "Derrota", $"Tu mano {you}; crupier {dealer}."); return state; }
    private static int Card(CareerState state) => 1 + NextInt(state, 10); private static int HandValue(List<int> cards) => cards.Sum();
    private void FinishCasinoHand(CareerState state, decimal net, string result, string detail)
    { var s = state.CasinoSession!; s.HandsPlayed++; s.NetResult += net; s.HandInProgress = false; AddLedger(state, "Casino ficticio", $"{s.Game}: {result}", net); state.CasinoHistory.Add(new CasinoHand { Season = state.Season, Game = s.Game, BetEur = s.BetEur, NetEur = net, Result = result, Detail = detail }); state.LastResolution = new EventResolution { Headline = "Casino ficticio", Result = result, Detail = detail + " Dinero ficticio de la partida.", Effects = [new() { Label = "Saldo", Value = (int)net, Direction = net >= 0 ? "positive" : "negative" }] }; }
    private void CloseCasino(CareerState state) { var s = state.CasinoSession!; state.ActivityCooldowns["casino"] = state.CurrentMatchday + 2; if (s.NetResult < 0 || state.CasinoHistory.Count(x => x.Season == state.Season) >= 8) { state.Player.Morale = Floor(state.Player.Morale - 2); state.Player.Energy = Floor(state.Player.Energy - 2); state.Lifestyle.Discipline = Floor(state.Lifestyle.Discipline - 3); state.Lifestyle.PublicImage = Floor(state.Lifestyle.PublicImage - 2); } state.Timeline.Add($"Casino ficticio: {s.HandsPlayed} manos, resultado €{s.NetResult:N0}."); state.CasinoSession = null; }

    private static void Spend(CareerState state, decimal amount, string category, string description)
    {
        if (state.Player.Money < amount) throw new InvalidOperationException("No tienes saldo suficiente para esta actividad.");
        AddLedger(state, category, description, -amount);
    }

    private static CareerState CompleteActivity(CareerState state, string title, string detail, List<EventEffect> effects)
    {
        state.LastResolution = new EventResolution { Headline = title, Result = "Actividad completada", Detail = detail, Effects = effects };
        state.Timeline.Add(detail);
        return state;
    }

    private bool ShouldCreateWorldClubEvent(CareerState state, bool matchSuccess)
    {
        var profile = state.WorldClubs.FirstOrDefault(x => x.Club == state.CurrentClub);
        return profile is not null && (matchSuccess || profile.FinancialTier >= 3) && Next(state) < .22;
    }

    private SeasonEvent CreateWorldClubEvent(CareerState state, MatchContext match)
    {
        var profile = state.WorldClubs.First(x => x.Club == state.CurrentClub);
        var arrival = state.WorldTransfers.LastOrDefault(x => x.ToClub == state.CurrentClub);
        var title = arrival is null ? "La directiva define el proyecto" : $"Mercado: llega {arrival.PlayerName}";
        var description = arrival is null
            ? $"El presupuesto del club es de €{profile.TransferBudgetEur:N0} y la directiva exige resultados. Tu posición en el proyecto puede cambiar tras el partido ante {match.Rival}."
            : $"{arrival.PlayerName}, {arrival.Position.ToLowerInvariant()}, llega desde {arrival.FromClub}. La competencia por minutos cambia el vestuario y tu rol.";
        return OffPitch($"world-{state.Season}-{state.CurrentMatchday}", "Mundo", title, description, "Decisión de vestuario",
            ("embrace", "Integrar el proyecto y competir", "Bajo"), ("challenge", "Exigir garantías de minutos", "Alto"));
    }

    private CareerState ResolveWorldClubEvent(CareerState state, SeasonEvent active, EventOption option)
    {
        var p = state.Player; state.WorldEventUsed = true; var success = Next(state) < (option.Id == "embrace" ? .82 : .48 + p.Reputation / 300d);
        if (option.Id == "embrace")
        {
            p.CoachRelation = Cap(p.CoachRelation + 4); p.Morale = Cap(p.Morale + 3); p.Reputation += 1;
            active.Outcome = "Aceptaste la competencia y el técnico valoró tu actitud dentro del nuevo proyecto.";
        }
        else if (success)
        {
            p.CoachRelation = Cap(p.CoachRelation + 1); p.Reputation += 3; p.Morale = Cap(p.Morale + 2);
            active.Outcome = "La conversación fue firme pero productiva: conservas un rol relevante y sube tu visibilidad.";
        }
        else
        {
            p.CoachRelation = Floor(p.CoachRelation - 6); p.Morale = Floor(p.Morale - 4); p.FansRelation = Floor(p.FansRelation - 2);
            active.Outcome = "La exigencia tensó la relación con el técnico y deja abierta la competencia por tu puesto.";
        }
        active.Resolution = new EventResolution { Headline = active.Title, Result = success ? "Proyecto fortalecido" : "Tensión en el proyecto", Detail = active.Outcome, Effects = [new() { Label = "Técnico", Value = option.Id == "embrace" ? 4 : success ? 1 : -6, Direction = success ? "positive" : "negative" }, new() { Label = "Moral", Value = success ? 3 : -4, Direction = success ? "positive" : "negative" }] };
        state.LastResolution = active.Resolution; state.CompletedEvents.Add(active); state.Timeline.Add(active.Outcome); state.ActiveEvent = null;
        return state;
    }

    private CareerState ResolveOffPitchEvent(CareerState state, SeasonEvent active, EventOption option)
    {
        if (active.Category == "Selección") return ResolveNationalEvent(state, active, option);
        if (active.Category == "Mundo") return ResolveWorldClubEvent(state, active, option);
        if (active.Category == "Entrenamiento" && option.Id.StartsWith("drill:")) return ResolveTrainingPlan(state, active, option);
        var p = state.Player;
        var chance = Math.Clamp(.42 + p.Morale / 300d + p.CoachRelation / 450d - RiskPenalty(option.Risk), .18, .9);
        var success = Next(state) < chance;
        string outcome;
        if (active.Category == "Entrenamiento")
        {
            if (option.Id == "intense")
            {
                if (success) { p.Pace = Cap(p.Pace + 2); p.Physical = Cap(p.Physical + 2); p.Energy = Floor(p.Energy - 10); p.InjuryRisk = Cap(p.InjuryRisk + 5); outcome = "La doble sesión dio resultado: subieron ritmo y físico, aunque aumentó el riesgo de lesión."; }
                else { p.Energy = Floor(p.Energy - 18); p.InjuryRisk = Cap(p.InjuryRisk + 13); p.Form = Floor(p.Form - 4); outcome = "La carga fue excesiva: perdiste energía, forma y el riesgo de lesión aumentó."; }
            }
            else { p.Passing = Cap(p.Passing + 1); p.Dribbling = Cap(p.Dribbling + 1); p.Energy = Floor(p.Energy - 3); p.CoachRelation = Cap(p.CoachRelation + 2); outcome = "La sesión técnica mejoró pase y regate sin comprometer tu disponibilidad."; }
        }
        else if (active.Category == "Prensa")
        {
            if (option.Id == "team") { p.FansRelation = Cap(p.FansRelation + 4); p.CoachRelation = Cap(p.CoachRelation + 3); p.MediaRelation = Cap(p.MediaRelation + 2); p.Morale = Cap(p.Morale + 2); outcome = "Tu mensaje protegió al vestuario: técnico, afición y medios valoraron la responsabilidad."; }
            else { p.MediaRelation = Floor(p.MediaRelation - 8); p.CoachRelation = Floor(p.CoachRelation - 6); p.FansRelation = success ? Cap(p.FansRelation + 3) : Floor(p.FansRelation - 4); p.Reputation = success ? Cap(p.Reputation + 2) : Floor(p.Reputation - 3); outcome = success ? "La declaración encendió a la afición, pero tensó tu relación con el técnico y los medios." : "La declaración generó polémica: técnico, medios y parte de la afición reaccionaron mal."; }
        }
        else if (active.Category == "Finanzas")
        {
            var amount = Math.Max(250m, p.Money * (option.Id == "safe" ? .015m : success ? .06m : -.08m));
            AddLedger(state, "Finanzas", "Movimiento financiero", amount);
            outcome = amount >= 0 ? $"La decisión financiera dejó €{amount:N0} en tu cuenta." : $"La inversión costó €{Math.Abs(amount):N0}.";
        }
        else
        {
            if (option.Id == "rest") { p.Energy = Cap(p.Energy + 13); p.InjuryRisk = Floor(p.InjuryRisk - 10); p.Form = Floor(p.Form - 1); outcome = "La recuperación bajó el riesgo de lesión y te devolvió energía, aunque perdiste algo de ritmo."; }
            else if (success) { p.Physical = Cap(p.Physical + 1); p.Energy = Floor(p.Energy - 7); p.InjuryRisk = Cap(p.InjuryRisk + 8); outcome = "Aguantaste la carga y ganaste físico, pero el cuerpo queda expuesto."; }
            else { p.Energy = Floor(p.Energy - 16); p.InjuryRisk = Cap(p.InjuryRisk + 20); p.Form = Floor(p.Form - 5); outcome = "Forzaste la situación y pagaste el precio: bajaron energía y forma; el riesgo de lesión se disparó."; }
        }
        RecalculateOverall(p); active.Outcome = outcome;
        active.Resolution = new EventResolution { Headline = active.Title, Result = success ? "Decisión favorable" : "Decisión con coste", Detail = outcome, Effects = EffectsFor(active.Category, p, success) };
        state.LastResolution = active.Resolution; state.CompletedEvents.Add(active); RecordTemplate(state, active.TemplateId ?? active.Id); state.Timeline.Add(outcome); state.ActiveEvent = null;
        return state;
    }

    private SeasonEvent CreatePreseasonEvent(CareerState state)
    {
        var p = state.Player;
        return new SeasonEvent
        {
            Id = $"preseason-{state.Season}", Category = "Pretemporada", Title = "Plan de pretemporada",
            Description = $"El cuerpo técnico revisó tu campaña anterior: {p.LastSeasonAppearances} PJ, {p.LastSeasonGoals} goles, {p.LastSeasonAssists} asistencias y promedio {p.LastSeasonAverage:0.00}. Elige una rutina: dos ofrecen progreso y una supone riesgo.",
            MiniGame = "Rutina de pretemporada", Options = CreateTrainingPlanOptions(state), RequiredSelections = 1
        };
    }

    private CareerState ResolvePreseason(CareerState state, SeasonEvent active, string optionId)
    {
        var option = active.Options.FirstOrDefault(candidate => candidate.Id == optionId)
            ?? throw new InvalidOperationException("La rutina elegida no pertenece a la pretemporada.");
        return ResolveTrainingPlan(state, active, option);
    }

    private List<EventOption> CreateTrainingPlanOptions(CareerState state)
    {
        var attributes = TrainingAttributes(state.Player).OrderBy(_ => Next(state)).Take(3).ToList();
        return attributes.Select((attribute, index) => new EventOption
        {
            Id = $"drill:{(index == 2 ? "risk" : "boost")}:{attribute}",
            Label = index == 2 ? $"Forzar {PreseasonLabel(attribute)}" : $"Potenciar {PreseasonLabel(attribute)}",
            Risk = index == 2 ? "Riesgo de retroceso" : "Progreso probable"
        }).ToList();
    }

    private CareerState ResolveTrainingPlan(CareerState state, SeasonEvent active, EventOption option)
    {
        var parts = option.Id.Split(':');
        if (parts.Length != 3) throw new InvalidOperationException("Rutina de entrenamiento inválida.");
        var positive = parts[1] == "boost"; var focus = parts[2]; var p = state.Player;
        var affected = TrainingAttributes(p).OrderBy(_ => Next(state)).Take(1 + NextInt(state, 4)).ToList();
        if (!affected.Contains(focus)) affected[0] = focus;
        var reports = new List<string>();
        foreach (var attribute in affected.Distinct())
        {
            var delta = 1 + NextInt(state, 4);
            AdjustAttribute(p, attribute, positive ? delta : -delta);
            reports.Add($"{PreseasonLabel(attribute)} {(positive ? "+" : "-")}{delta}");
        }
        p.Energy = Floor(p.Energy + (positive ? -4 : -8));
        p.InjuryRisk = Cap(p.InjuryRisk + (positive ? 2 : 7));
        p.Morale = Cap(p.Morale + (positive ? 3 : -4)); RecalculateOverall(p);
        active.Outcome = $"{(active.Category == "Pretemporada" ? "Pretemporada" : "Entrenamiento")} resuelto: {string.Join(" · ", reports)}. Se afectaron {reports.Count} atributo(s); media {p.Overall}.";
        active.Resolution = new EventResolution { Headline = active.Title, Result = positive ? "Progreso" : "Retroceso", Detail = active.Outcome, Effects = reports.Select(x => new EventEffect { Label = x, Direction = positive ? "positive" : "negative" }).ToList() };
        state.LastResolution = active.Resolution;
        state.CompletedEvents.Add(active); state.Timeline.Add(active.Outcome); state.ActiveEvent = null;
        return state;
    }

    private static IEnumerable<string> TrainingAttributes(Player p) => p.Position == "Portero"
        ? new[] { "goalkeeping", "passing", "physical", "dribbling" }
        : new[] { "pace", "shooting", "passing", "dribbling", "defending", "physical" };

    private static IEnumerable<string> SuggestedPreseasonAttributes(Player p)
    {
        var all = p.Position == "Portero"
            ? new[] { "goalkeeping", "passing", "physical", "dribbling" }
            : new[] { "pace", "shooting", "passing", "dribbling", "defending", "physical" };
        return all.OrderBy(attribute => AttributeValue(p, attribute)).ThenBy(attribute => attribute);
    }

    private static double PreseasonNeed(string attribute, Player p, CareerState state)
    {
        var valueNeed = (75 - AttributeValue(p, attribute)) / 100d;
        var roleNeed = p.Position switch
        {
            "Delantero" when attribute == "shooting" => .25,
            "Extremo" when attribute is "pace" or "dribbling" => .25,
            "Mediocampista" when attribute is "passing" or "dribbling" => .22,
            "Defensa" when attribute is "defending" or "physical" => .25,
            "Portero" when attribute == "goalkeeping" => .3,
            _ => 0
        };
        var formNeed = p.LastSeasonAverage < 6.4 ? .12 : 0;
        return valueNeed + roleNeed + formNeed + Next(state) * .06;
    }

    private static string PreseasonLabel(string attribute) => attribute switch
    {
        "pace" => "Ritmo", "shooting" => "Tiro", "passing" => "Pase", "dribbling" => "Regate",
        "defending" => "Defensa", "physical" => "Físico", _ => "Portería"
    };
    private static int AttributeValue(Player p, string attribute) => attribute switch
    {
        "pace" => p.Pace, "shooting" => p.Shooting, "passing" => p.Passing, "dribbling" => p.Dribbling,
        "defending" => p.Defending, "physical" => p.Physical, _ => p.Goalkeeping
    };
    private static void AdjustAttribute(Player p, string attribute, int delta)
    {
        if (attribute == "pace") p.Pace = Cap(p.Pace + delta);
        else if (attribute == "shooting") p.Shooting = Cap(p.Shooting + delta);
        else if (attribute == "passing") p.Passing = Cap(p.Passing + delta);
        else if (attribute == "dribbling") p.Dribbling = Cap(p.Dribbling + delta);
        else if (attribute == "defending") p.Defending = Cap(p.Defending + delta);
        else if (attribute == "physical") p.Physical = Cap(p.Physical + delta);
        else p.Goalkeeping = Cap(p.Goalkeeping + delta);
    }

    private List<IndividualAward> DetermineIndividualAwards(CareerState state, int leaguePosition, List<string> seasonTitles)
    {
        var p = state.Player; var awards = new List<IndividualAward>();
        var league = League(state); var titleBoost = seasonTitles.Count > 0 ? 1 : 0;
        void Award(string name, string scope, string reason, int reputation, decimal prize)
        {
            var award = new IndividualAward { Season = state.Season, Name = name, Scope = scope, Reason = reason, ReputationGain = reputation, PrizeEur = prize };
            awards.Add(award); state.IndividualAwards.Add(award);
            p.Reputation = Math.Min(100, p.Reputation + reputation); p.Morale = Math.Min(95, p.Morale + Math.Max(3, reputation / 2));
            p.FansRelation = Math.Min(100, p.FansRelation + reputation); p.MediaRelation = Math.Min(100, p.MediaRelation + reputation + 2);
            if (prize > 0) AddLedger(state, "Premio individual", name, prize);
            state.Timeline.Add($"Premio individual: {name}. {reason}");
        }
        var appearanceFloor = Math.Max(8, state.Fixtures.Count / 3);
        if (p.LastSeasonAppearances < appearanceFloor) return awards;
        var goalTarget = GoldenBootTarget(league); var assistTarget = PlaymakerTarget(league); var contributions = p.LastSeasonGoals + p.LastSeasonAssists;
        if (p.LastSeasonAverage >= 7.15) Award("Equipo de la temporada", state.CurrentLeague, $"Media {p.LastSeasonAverage:0.00} en {p.LastSeasonAppearances} partidos.", 4, league.MarketScaleEur * .002m);
        if (p.Age <= 22 && p.LastSeasonAverage >= 7.2 && (contributions >= 5 || (p.Position is "Defensa" or "Portero" && leaguePosition <= 6))) Award("Mejor jugador joven", state.CurrentLeague, $"Impacto de élite antes de los 22 años.", 7, league.MarketScaleEur * .005m);
        if (p.Position is "Delantero" or "Extremo" && p.LastSeasonGoals >= goalTarget && p.LastSeasonAverage >= 7.1) Award("Bota de Oro de liga", state.CurrentLeague, $"{p.LastSeasonGoals} goles; el estándar de tu liga era {goalTarget}+.", 8, league.MarketScaleEur * .008m);
        if (p.Position == "Mediocampista" && p.LastSeasonAssists >= assistTarget && p.LastSeasonAverage >= 7.15) Award("Mejor asistidor", state.CurrentLeague, $"{p.LastSeasonAssists} asistencias; el estándar de tu liga era {assistTarget}+.", 7, league.MarketScaleEur * .006m);
        if (p.Position == "Defensa" && p.LastSeasonAverage >= 7.3 && leaguePosition <= 6) Award("Defensa del año", state.CurrentLeague, $"Regularidad defensiva y clasificación final {leaguePosition}º.", 7, league.MarketScaleEur * .006m);
        if (p.Position == "Portero" && p.LastSeasonAverage >= 7.35 && leaguePosition <= 6) Award("Portero del año", state.CurrentLeague, $"Intervenciones decisivas en un equipo top 6.", 7, league.MarketScaleEur * .006m);
        if (p.LastSeasonAverage >= 7.45 && leaguePosition <= 4 && (contributions >= (league.Prestige >= 82 ? 18 : 12) || p.Position is "Defensa" or "Portero")) Award("Jugador de la temporada", state.CurrentLeague, $"Media {p.LastSeasonAverage:0.00}, equipo en top {leaguePosition} y rendimiento decisivo.", 12, league.MarketScaleEur * .014m);
        if (league.Prestige >= 82 && p.Overall >= 84 && p.LastSeasonAverage >= 7.55 && seasonTitles.Count > 0 && p.NationalAppearances >= 6) Award("Balón de Oro", "Mundo", $"Temporada de élite: media {p.LastSeasonAverage:0.00}, título de club, media FIFA {p.Overall} y peso con tu selección.", 20, league.MarketScaleEur * .035m);
        return awards;
    }

    private static int GoldenBootTarget(League league) => league.Prestige switch { >= 88 => 24, >= 80 => 20, >= 68 => 16, _ => 13 };
    private static int PlaymakerTarget(League league) => league.Prestige switch { >= 88 => 14, >= 80 => 11, >= 68 => 9, _ => 7 };

    private static void SaveLastSeasonPerformance(CareerState state)
    {
        var p = state.Player;
        p.LastSeasonAppearances = p.Appearances - state.SeasonStartAppearances;
        p.LastSeasonGoals = p.Goals - state.SeasonStartGoals;
        p.LastSeasonAssists = p.Assists - state.SeasonStartAssists;
        var ratings = p.RatingTotal - state.SeasonStartRatingTotal;
        p.LastSeasonAverage = p.LastSeasonAppearances == 0 ? 5.8 : ratings / p.LastSeasonAppearances;
    }

    private void ApplySeasonDevelopment(CareerState state, int leaguePosition, List<string> seasonTitles)
    {
        var p = state.Player;
        var start = state.SeasonStartOverall is >= 40 and <= 99 ? state.SeasonStartOverall : p.Overall;
        var scheduledMatches = Math.Max(1, MatchdayCount(state));
        var participation = p.LastSeasonAppearances / (double)scheduledMatches;
        var contributions = p.LastSeasonGoals + p.LastSeasonAssists;
        var production = p.Position switch
        {
            "Delantero" or "Extremo" => contributions >= Math.Max(8, scheduledMatches / 2),
            "Mediocampista" => contributions >= Math.Max(6, scheduledMatches / 3),
            "Defensa" or "Portero" => leaguePosition <= Math.Max(6, League(state).ClubNames.Count / 3),
            _ => false
        };
        var breakout = p.Age <= 21 && participation >= .55 && p.LastSeasonAverage >= 7.15 && (production || seasonTitles.Count > 0);
        var excellent = participation >= .48 && p.LastSeasonAverage >= 6.8 && (production || leaguePosition <= 5);
        var good = participation >= .35 && p.LastSeasonAverage >= 6.35;
        var poor = p.LastSeasonAppearances > 0 && p.LastSeasonAverage < 5.65 || p.Age >= 20 && participation < .16;
        var severe = p.InjuryRisk >= 72 || p.LastSeasonAppearances == 0 && p.Age >= 22;

        var potentialChange = breakout ? 4 + NextInt(state, 4)
            : excellent ? 2 + NextInt(state, 3)
            : good && p.Age <= 24 ? 1 + NextInt(state, 2)
            : severe ? -2 - NextInt(state, 2)
            : poor ? -1 : 0;
        p.Potential = Math.Clamp(Math.Max(p.Overall, p.Potential + potentialChange), 60, 99);

        var requestedDelta = breakout ? 5 + NextInt(state, 4)
            : excellent ? 3 + NextInt(state, 3)
            : good ? 1 + NextInt(state, 3)
            : severe ? -2 - NextInt(state, 3)
            : poor ? -1 - NextInt(state, 3)
            : p.Age <= 20 && participation >= .2 ? NextInt(state, 2)
            : 0;
        if (p.Age >= 30 && !breakout && !excellent) requestedDelta = Math.Min(requestedDelta, p.Age >= 34 ? -1 - NextInt(state, 2) : 0);

        var target = Math.Clamp(start + requestedDelta, 40, p.Potential);
        ShiftOverallThroughAttributes(p, target);
        p.LastSeasonOverallChange = p.Overall - start;
        p.DevelopmentTrajectory = p.Overall >= 90 ? "Clase mundial" : p.Overall >= 82 ? "Nivel élite" : p.LastSeasonOverallChange >= 5 ? "Salto de nivel" : p.LastSeasonOverallChange >= 2 ? "En crecimiento" : p.LastSeasonOverallChange <= -3 ? "Carrera en riesgo" : p.LastSeasonOverallChange < 0 ? "En declive" : "Estable";
        p.DevelopmentNote = p.LastSeasonOverallChange switch
        {
            >= 5 => $"Temporada de impacto: tus actuaciones elevan la media de {start} a {p.Overall} y abren un techo de {p.Potential}.",
            >= 2 => $"La regularidad transforma rendimiento en atributos: {start} → {p.Overall}; tu proyección llega a {p.Potential}.",
            > 0 => $"Sumaste progreso real en atributos clave. Media {start} → {p.Overall}.",
            <= -3 => $"La falta de continuidad o el desgaste golpearon tu nivel: {start} → {p.Overall}. Aún puedes reconstruirte.",
            < 0 => $"La temporada frenó tu evolución: {start} → {p.Overall}. Recuperar minutos será decisivo.",
            _ => $"Tu nivel se mantuvo en {p.Overall}; una campaña más dominante puede abrir el siguiente salto."
        };
        state.CareerMilestones.Add($"{state.Season}: {p.DevelopmentTrajectory}. {p.DevelopmentNote}");
    }

    private static void ShiftOverallThroughAttributes(Player p, int target)
    {
        var attributes = DevelopmentAttributes(p).ToList();
        var direction = target >= p.Overall ? 1 : -1;
        var guard = 0;
        while ((direction > 0 ? p.Overall < target : p.Overall > target) && guard++ < 360)
        {
            var attribute = attributes[guard % attributes.Count];
            AdjustAttribute(p, attribute, direction);
            RecalculateOverall(p);
            if (direction > 0 && p.Overall >= p.Potential) break;
        }
    }

    private static IEnumerable<string> DevelopmentAttributes(Player p) => p.Position switch
    {
        "Portero" => new[] { "goalkeeping", "goalkeeping", "goalkeeping", "physical", "passing", "goalkeeping", "dribbling" },
        "Defensa" => new[] { "defending", "defending", "physical", "defending", "pace", "passing", "dribbling" },
        "Mediocampista" => new[] { "passing", "dribbling", "passing", "pace", "defending", "shooting", "physical" },
        "Extremo" => new[] { "pace", "dribbling", "shooting", "pace", "dribbling", "passing", "physical" },
        _ => new[] { "shooting", "shooting", "pace", "dribbling", "shooting", "physical", "passing" }
    };

    private List<TableRow> BuildTable(CareerState state)
    {
        var teams = LeagueTeams(state);
        return teams.Select(name => new TableRow { Club = name }).ToList();
    }

    // The catalog is an initial snapshot; promotions, relegations and transfers alter league membership during a career.
    private List<string> LeagueTeams(CareerState state)
    {
        var teams = _world.Clubs.Where(c => c.League == state.CurrentLeague).Select(c => c.Name).ToList();
        if (!teams.Contains(state.CurrentClub))
        {
            if (teams.Count > 0) teams.RemoveAt(teams.Count - 1);
            teams.Add(state.CurrentClub);
        }
        return teams;
    }

    private void UpdateTable(CareerState state)
    {
        var rows = BuildTable(state).ToDictionary(r => r.Club);
        foreach (var fixture in state.Fixtures.Where(f => f.Competition == state.CurrentLeague && f.IsPlayed && f.HomeGoals.HasValue && f.AwayGoals.HasValue))
        {
            var home = rows[fixture.Home]; var away = rows[fixture.Away]; var hg = fixture.HomeGoals!.Value; var ag = fixture.AwayGoals!.Value;
            home.Played++; away.Played++; home.GoalsFor += hg; home.GoalsAgainst += ag; away.GoalsFor += ag; away.GoalsAgainst += hg;
            if (hg > ag) { home.Wins++; home.Points += 3; away.Losses++; } else if (ag > hg) { away.Wins++; away.Points += 3; home.Losses++; } else { home.Draws++; away.Draws++; home.Points++; away.Points++; }
        }
        state.Table = rows.Values.Select(r => { r.GoalDifference = r.GoalsFor - r.GoalsAgainst; return r; }).OrderByDescending(r => r.Points).ThenByDescending(r => r.GoalDifference).ThenByDescending(r => r.GoalsFor).ToList();
    }

    private CareerState ResolveTransfer(CareerState state, string optionId)
    {
        if (optionId == "wait") { state.Timeline.Add("Decidiste esperar una oferta mejor."); state.TransferOffers.Clear(); state.ActiveEvent = null; return state; }
        var offer = state.TransferOffers.FirstOrDefault(x => x.Club == optionId) ?? throw new InvalidOperationException("Oferta no válida.");
        state.PendingClub = offer.Club; state.PendingLeague = offer.League;
        state.Timeline.Add($"Acordaste llegar a {offer.Club} para jugar en {offer.League} al finalizar la temporada."); state.TransferOffers.Clear(); state.ActiveEvent = null;
        return state;
    }

    private SeasonEvent TransferEvent(CareerState state) => new()
    {
        Id = "transfer", Category = "Transferencias", Title = "El mercado reacciona a tu actuación", MiniGame = "Negociación",
        Description = "Tu rendimiento en un partido importante activó el interés de otros clubes durante la ventana de mercado.",
        Options = [.. state.TransferOffers.Select(o => new EventOption { Id = o.Club, Label = $"Firmar con {o.Club} · €{o.Salary:N0}/año", Risk = o.Role }), new EventOption { Id = "wait", Label = "Esperar una oferta mejor", Risk = "Bajo" }]
    };

    private List<TransferOffer> GenerateTransferOffers(CareerState state)
    {
        RefreshMarketProfile(state);
        var current = Club(state.CurrentClub);
        var currentStrength = state.WorldClubs.FirstOrDefault(x => x.Club == state.CurrentClub)?.SquadStrength ?? current?.Prestige ?? 45;
        var candidates = state.WorldClubs.Count == 0 ? BuildWorldProfiles(state) : state.WorldClubs;
        var p = state.Player; var exceptional = p.Age <= 21 && p.LastSeasonAverage >= 7.35 && (p.LastSeasonGoals + p.LastSeasonAssists >= 12 || p.Position is "Defensa" or "Portero");
        var eligible = candidates.Where(c => c.Club != state.CurrentClub && c.TransferBudgetEur > 0)
            .Select(c => new { Club = c, Required = RequiredOverall(c), Gap = p.Overall - RequiredOverall(c), Compatibility = MarketCompatibility(state, c) })
            .Where(x => x.Gap >= 0 || (exceptional && x.Gap >= -3))
            .Where(x => x.Compatibility >= 48)
            .Where(x => x.Club.SquadStrength <= currentStrength + 13 || p.LastSeasonAverage >= 7.2 || exceptional)
            .OrderByDescending(x => x.Compatibility + x.Gap * 4 + Math.Min(10, x.Club.SquadStrength - currentStrength) + NextInt(state, 28))
            .Take(5).ToList();
        return eligible.Select(x =>
        {
            var club = Club(x.Club.Club)!; var rival = club.League == state.CurrentLeague && Math.Abs(club.Prestige - (current?.Prestige ?? 50)) <= 18;
            var salary = AnnualSalary(state, club) * (rival ? 1.15m : 1m);
            var role = p.Overall >= x.Club.SquadStrength + 3 ? "Titular" : p.Overall >= x.Club.SquadStrength - 3 ? "Rotación" : "Competencia alta";
            var years = 2 + NextInt(state, 4);
            return new TransferOffer { Club = club.Name, League = club.League, Salary = salary, MonthlyNetEur = Math.Round(salary * NetRate(club.League) / 12m), SigningBonusEur = Math.Round(salary * .08m), Role = role, ClubBudgetEur = x.Club.TransferBudgetEur, ClubStrength = x.Club.SquadStrength, RequiredOverall = x.Required, Compatibility = x.Compatibility, MarketTier = MarketTier(x.Club.SquadStrength), Need = $"Busca {p.Position.ToLowerInvariant()} con proyección", Reason = rival ? "Rival directo: ofrece más salario, pero la afición reaccionará" : TransferReason(state, x.Club, exceptional), IsRival = rival, ContractYears = years, ReleaseClauseEur = Math.Round(salary * years * 1.8m), Adaptation = club.League == state.CurrentLeague ? "Inmediata" : club.Prestige >= currentStrength + 10 ? "Exigente" : "Normal" };
        }).ToList();
    }

    private void RefreshMarketProfile(CareerState state)
    {
        var p = state.Player;
        var seasonMatches = Math.Max(1, p.Appearances - state.SeasonStartAppearances);
        var seasonContributions = (p.Goals - state.SeasonStartGoals) + (p.Assists - state.SeasonStartAssists);
        var average = seasonMatches == 0 ? p.LastSeasonAverage : (p.RatingTotal - state.SeasonStartRatingTotal) / seasonMatches;
        var score = (int)Math.Clamp(p.Overall * .45 + p.Form * .12 + p.Reputation * .16 + p.Morale * .05 + Math.Max(0, average - 6) * 7 + Math.Min(12, seasonContributions) * .8 + (p.Age <= 22 ? 5 : 0) - p.InjuryRisk * .08, 25, 99);
        var value = Math.Round(Math.Clamp((decimal)Math.Pow(Math.Max(1, p.Overall - 42), 2.6) * 2300m * (1m + p.Reputation / 100m) * (p.Age <= 22 ? 1.25m : p.Age > 31 ? .65m : 1m), 25_000m, 180_000_000m) / 1_000m) * 1_000m;
        var candidates = state.WorldClubs.Count == 0 ? BuildWorldProfiles(state) : state.WorldClubs;
        var scouts = candidates.Where(c => p.Overall >= RequiredOverall(c) - 4 && MarketCompatibility(state, c) >= 52).OrderByDescending(c => MarketCompatibility(state, c)).Take(4).Select(c => c.Club).ToList();
        state.MarketProfile = new PlayerMarketProfile { Score = score, EstimatedValueEur = value, InterestLevel = score >= 78 ? "Interés internacional" : score >= 66 ? "Seguimiento activo" : score >= 54 ? "Radar regional" : "Construyendo reputación", ScoutingClubs = scouts, Summary = $"Media {p.Overall}, forma {p.Form} y promedio {average:0.00}. {scouts.Count} clubes encajan con tu perfil actual." };
    }

    private static int RequiredOverall(ClubWorldProfile club) => club.SquadStrength switch { >= 88 => 82, >= 82 => 76, >= 74 => 68, >= 64 => 60, _ => 52 };
    private static string MarketTier(int strength) => strength switch { >= 88 => "Élite global", >= 82 => "Champions", >= 74 => "Primera fuerte", >= 64 => "Primera/ascenso", _ => "Desarrollo" };
    private static int MarketCompatibility(CareerState state, ClubWorldProfile club)
    {
        var p = state.Player; var age = p.Age <= 22 ? 8 : p.Age > 32 ? -5 : 2;
        var profile = club.RecruitmentProfile;
        var style = profile.Contains("Talento") && p.Age <= 23 ? 7 : profile.Contains("Exportador") && p.Age <= 25 ? 5 : profile.Contains("Equilibrado") ? 3 : 0;
        return (int)Math.Clamp(48 + (p.Overall - RequiredOverall(club)) * 5 + p.Reputation / 3 + p.Form / 12 + age + style - p.InjuryRisk / 4, 0, 100);
    }
    private static string TransferReason(CareerState state, ClubWorldProfile club, bool exceptional) => exceptional && state.Player.Overall < RequiredOverall(club) ? "Tu temporada excepcional abre una excepción juvenil" : state.Player.Overall >= club.SquadStrength ? "Tu media ya compite por un puesto" : "Tu rendimiento y proyección encajan con el rol";

    private Contract CreateContract(CareerState state, Club club, string role)
    {
        var annual = AnnualSalary(state, club);
        return new Contract { Club = club.Name, League = club.League, AnnualGrossEur = annual, MonthlyNetEur = Math.Round(annual * NetRate(club.League) / 12m), SigningBonusEur = Math.Round(annual * .08m), AppearanceBonusEur = Math.Max(150m, Math.Round(annual * .002m)), GoalOrAssistBonusEur = Math.Max(100m, Math.Round(annual * .0015m)), TitleBonusEur = Math.Round(annual * .12m), Role = role, Season = state.Season };
    }

    private decimal AnnualSalary(CareerState state, Club club)
    {
        var league = _world.Leagues.First(x => x.Name == club.League);
        var baseSalary = Math.Max(8_000m, league.MarketScaleEur * .015m);
        var playerFactor = Math.Max(.35m, (state.Player.Overall - 42) / 42m) * (1m + state.Player.Reputation / 140m);
        var ageFactor = state.Player.Age < 19 ? .55m : state.Player.Age > 33 ? .78m : 1m;
        var financial = Math.Max(.55m, club.FinancialTier / 3m);
        return Math.Round(Math.Clamp(baseSalary * playerFactor * ageFactor * financial * (club.Prestige / 70m), 8_000m, 18_000_000m) / 500m) * 500m;
    }

    private static decimal NetRate(string league) => league switch { "Premier League" => .56m, "LALIGA EA SPORTS" or "LALIGA HYPERMOTION" => .52m, "Liga MX" or "Liga de Expansión MX" => .61m, _ => .58m };

    private void PaySalaryUntil(CareerState state, int month)
    {
        if (state.Contract is null) return;
        while (state.SalaryMonthsPaid < month)
        {
            state.SalaryMonthsPaid++;
            AddLedger(state, "Salario", $"Nómina mensual · mes {state.SalaryMonthsPaid}", state.Contract.MonthlyNetEur, state.SalaryMonthsPaid.ToString());
        }
    }

    private static void AddLedger(CareerState state, string category, string description, decimal amount, string? month = null)
    {
        state.Player.Money += amount;
        state.Ledger.Add(new FinancialLedgerEntry { Season = state.Season, Month = month ?? "pretemporada", Category = category, Description = description, AmountEur = amount, BalanceAfter = state.Player.Money });
    }

    private SeasonEvent ContractEvent(CareerState state)
    {
        var current = Club(state.CurrentClub)!;
        var renewal = CreateContract(state, current, state.Player.CoachRelation > 65 ? "Titular" : "Rotación");
        var offers = state.TransferOffers.Take(2).ToList();
        return new SeasonEvent
        {
            Id = $"contract-{state.Season}", Category = "Contrato", Title = "Tu contrato anual termina", MiniGame = "Negociación", Rarity = "rare",
            Description = $"Cerraste la temporada con {state.Player.LastSeasonAppearances} PJ, {state.Player.LastSeasonGoals} goles y media {state.Player.LastSeasonAverage:0.00}. Decide tu siguiente paso.",
            Options = [new EventOption { Id = "renew", Label = $"Renovar con {current.Name} · €{renewal.AnnualGrossEur:N0}/año", Risk = renewal.Role }, .. offers.Select(x => new EventOption { Id = x.Club, Label = $"Ir a {x.Club} · €{x.Salary:N0}/año", Risk = x.Role }), new EventOption { Id = "market", Label = "Pedir a tu agente buscar salida", Risk = "Mercado abierto" }]
        };
    }

    private CareerState ResolveContract(CareerState state, string optionId)
    {
        var active = state.ActiveEvent!;
        Club destination;
        if (optionId == "renew") destination = Club(state.CurrentClub)!;
        else if (optionId == "market") destination = _world.Clubs.Where(c => c.Name != state.CurrentClub).OrderBy(c => Math.Abs(c.Prestige - Club(state.CurrentClub)!.Prestige)).First();
        else destination = _world.Clubs.FirstOrDefault(c => c.Name == optionId) ?? throw new InvalidOperationException("Club no válido.");
        var previousClub = Club(state.CurrentClub)!;
        var moved = destination.Name != state.CurrentClub;
        state.CurrentClub = destination.Name; state.CurrentLeague = destination.League;
        state.Contract = CreateContract(state, destination, moved ? "Nuevo fichaje" : "Renovado");
        AddLedger(state, "Firma", $"Bono de firma con {destination.Name}", state.Contract.SigningBonusEur);
        var rivalMove = moved && destination.League == previousClub.League && Math.Abs(destination.Prestige - previousClub.Prestige) <= 18;
        if (rivalMove) { state.Player.FansRelation = Floor(state.Player.FansRelation - 12); state.Player.MediaRelation = Floor(state.Player.MediaRelation - 5); state.Player.Morale = Floor(state.Player.Morale - 2); }
        var adaptationMessage = destination.League == previousClub.League ? "inmediata" : "a una nueva liga";
        active.Outcome = moved
            ? rivalMove
                ? $"Firmaste por el rival {destination.Name}. La afición de {previousClub.Name} toma la decisión como una ruptura."
                : $"Firmaste por {destination.Name}; comienza una adaptación {adaptationMessage}."
            : $"Renovaste con {destination.Name}.";
        active.Resolution = new EventResolution { Headline = active.Title, Result = moved ? "Traspaso acordado" : "Renovación acordada", Detail = $"Contrato anual por €{state.Contract.AnnualGrossEur:N0} brutos; depósito mensual neto de €{state.Contract.MonthlyNetEur:N0}.", Effects = [new EventEffect { Label = "Bono de firma", Value = (int)state.Contract.SigningBonusEur, Direction = "positive" }] };
        state.LastResolution = active.Resolution; state.CompletedEvents.Add(active); state.Timeline.Add(active.Outcome);
        state.Season++; state.EventIndex = 0; state.CurrentMatchday = 0; state.SeasonComplete = false; state.CompletedEvents = []; state.TransferOffers = [];
        InitializeSeason(state); state.Timeline.Add($"Comienza {state.Season} en {state.CurrentClub}."); state.ActiveEvent = CreatePreseasonEvent(state);
        return state;
    }

    private static List<EventEffect> EffectsFor(string category, Player player, bool success) => category switch
    {
        "Recuperación" => [new() { Label = "Energía", Value = success ? 13 : -16, Direction = success ? "positive" : "negative" }, new() { Label = "Riesgo de lesión", Value = success ? -10 : 20, Direction = success ? "positive" : "negative" }],
        "Prensa" => [new() { Label = "Relación con medios", Value = success ? 2 : -8, Direction = success ? "positive" : "negative" }],
        _ => [new() { Label = "Media", Value = player.Overall, Direction = "neutral" }]
    };

    private static EventResolution MatchResolution(CareerState state, SeasonEvent active, EventOption option, bool success, MatchFixture fixture)
    {
        var won = (fixture.HomeGoals > fixture.AwayGoals) == (fixture.Home == state.CurrentClub);
        var drawn = fixture.HomeGoals == fixture.AwayGoals;
        var result = drawn ? "Empate" : won ? "Victoria" : "Derrota";
        var score = $"{fixture.Home} {fixture.HomeGoals}–{fixture.AwayGoals} {fixture.Away}";
        var detail = success ? $"{option.Label}: {OutcomeText(option.ActionType)}." : $"{option.Label}: {option.FailureOutcome}.";
        return new EventResolution { Headline = active.Title, Result = result, Score = score, Detail = detail, Effects = [new() { Label = "Forma", Value = success ? (option.ActionType is "goal" or "save" ? 8 : 5) : -5, Direction = success ? "positive" : "negative" }, new() { Label = "Energía", Value = success ? -3 : -7, Direction = "negative" }, new() { Label = "Fama", Value = success ? (option.ActionType is "goal" or "save" ? 5 : 3) : -1, Direction = success ? "positive" : "negative" }, new() { Label = "Moral", Value = success ? 5 : -3, Direction = success ? "positive" : "negative" }] };
    }

    private static string OutcomeText(string actionType) => actionType switch { "goal" => "la definición termina en gol", "assist" => "tu acción genera el gol del equipo", "save" => "sacas una intervención decisiva", "recovery" => "recuperas la posesión", "clear" => "alejas el peligro", _ => "creas una ocasión favorable" };

    private void ApplyLeagueMovement(CareerState state, int position)
    {
        var league = League(state);
        if (league.Tier == 2 && position > 0 && position <= league.PromotionCount && !string.IsNullOrWhiteSpace(league.PromotionLeague)) { state.CurrentLeague = league.PromotionLeague; state.Timeline.Add($"¡Ascenso! {state.CurrentClub} jugará en {state.CurrentLeague}."); }
        else if (league.Tier == 1 && league.RelegationCount > 0 && position > state.Table.Count - league.RelegationCount && !string.IsNullOrWhiteSpace(league.RelegationLeague)) { state.CurrentLeague = league.RelegationLeague; state.Timeline.Add($"Descenso: {state.CurrentClub} jugará en {state.CurrentLeague}."); }
    }

    private int MatchdayCount(CareerState state) => state.Fixtures.Count == 0 ? 0 : state.Fixtures.Max(f => f.Matchday);
    private League League(CareerState state) => _world.Leagues.First(l => l.Name == state.CurrentLeague);
    private Club? Club(string name) => _world.Clubs.FirstOrDefault(c => c.Name == name);
    private int Goals(CareerState state, int attack, int defense) => Math.Clamp((int)Math.Floor(Next(state) * 3 + (attack - defense) / 28d), 0, 5);
    private string StateOfMatch(CareerState state, string rival) => $"{(Club(rival)?.Prestige > (Club(state.CurrentClub)?.Prestige ?? 50) ? "Un rival superior te exige precisión." : "Es una oportunidad para sumar puntos importantes.")}";
    private static string DescribeMatchOutcome(CareerState state, SeasonEvent active, EventOption option, bool success, MatchFixture fixture)
    {
        var score = fixture.HomeGoals == fixture.AwayGoals ? "empate" : (fixture.HomeGoals > fixture.AwayGoals) == (fixture.Home == state.CurrentClub) ? "victoria" : "derrota";
        return success
            ? $"Minuto {active.Match!.Minute}: {option.Label} funcionó. El partido terminó en {score} y tu actuación mejoró forma y reputación."
            : $"Minuto {active.Match!.Minute}: {option.Label} no salió como esperabas. El partido terminó en {score}; la energía y la forma se resintieron.";
    }
    private bool IsTransferWindow(CareerState state) => state.CurrentMatchday <= 3 || Math.Abs(state.CurrentMatchday - MatchdayCount(state) / 2) <= 1;
    private static int StartingOverall(string position) => position == "Portero" ? 57 : position == "Delantero" ? 60 : 58;
    private static int StartingPotential(string archetype) => archetype switch { "Talento" => 90, "Incansable" or "Líder" => 86, _ => 84 };
    private List<int> SelectImportantMatchdays(CareerState state)
    {
        var playerFixtures = state.Fixtures.Where(f => f.Home == state.CurrentClub || f.Away == state.CurrentClub).ToList();
        var ownPrestige = Club(state.CurrentClub)?.Prestige ?? 50;
        return playerFixtures
            .Select(f =>
            {
                var rival = f.Home == state.CurrentClub ? f.Away : f.Home;
                var rivalPrestige = Club(rival)?.Prestige ?? 50;
                var lateSeasonWeight = f.Matchday / (double)Math.Max(1, MatchdayCount(state)) * .55;
                var rivalryWeight = Math.Abs(rivalPrestige - ownPrestige) / 45d;
                var profileWeight = state.Player.Form / 500d + state.Player.Reputation / 900d;
                return new { f.Matchday, Score = Next(state) + lateSeasonWeight + rivalryWeight + profileWeight };
            })
            .OrderByDescending(x => x.Score).Take(state.SeasonEventTarget).Select(x => x.Matchday).OrderBy(x => x).ToList();
    }

    private static void InitializeAttributes(Player p, CareerState state)
    {
        (p.Pace, p.Shooting, p.Passing, p.Dribbling, p.Defending, p.Physical, p.Goalkeeping) = p.Position switch
        {
            "Portero" => (44, 25, 50, 48, 20, 66, 62),
            "Defensa" => (64, 35, 54, 57, 64, 67, 18),
            "Mediocampista" => (65, 58, 65, 64, 53, 59, 16),
            "Extremo" => (74, 61, 59, 70, 34, 52, 14),
            _ => (70, 66, 54, 63, 30, 64, 14)
        };
        var variation = () => NextInt(state, 9) - 4;
        p.Pace = Cap(p.Pace + variation()); p.Shooting = Cap(p.Shooting + variation()); p.Passing = Cap(p.Passing + variation());
        p.Dribbling = Cap(p.Dribbling + variation()); p.Defending = Cap(p.Defending + variation()); p.Physical = Cap(p.Physical + variation()); p.Goalkeeping = Cap(p.Goalkeeping + variation());
        if (p.Archetype == "Talento") { p.Dribbling = Cap(p.Dribbling + 4); p.Passing = Cap(p.Passing + 2); }
        if (p.Archetype == "Incansable") { p.Physical = Cap(p.Physical + 4); p.Pace = Cap(p.Pace + 2); }
        if (p.Archetype == "Líder") { p.Passing = Cap(p.Passing + 2); p.Defending = Cap(p.Defending + 2); p.CoachRelation = 62; }
        p.SkillMoves = p.Archetype == "Talento" ? 4 : 3; p.WeakFoot = p.Archetype == "Equilibrado" ? 4 : 3;
        RecalculateOverall(p);
    }
    private static void RecalculateOverall(Player p)
    {
        var score = p.Position switch
        {
            "Portero" => p.Goalkeeping * .68 + p.Passing * .1 + p.Physical * .12 + p.Dribbling * .1,
            "Defensa" => p.Defending * .42 + p.Physical * .2 + p.Pace * .17 + p.Passing * .12 + p.Dribbling * .09,
            "Mediocampista" => p.Passing * .28 + p.Dribbling * .22 + p.Pace * .15 + p.Defending * .15 + p.Shooting * .1 + p.Physical * .1,
            "Extremo" => p.Pace * .27 + p.Dribbling * .26 + p.Shooting * .2 + p.Passing * .14 + p.Physical * .08 + p.Defending * .05,
            _ => p.Shooting * .32 + p.Pace * .2 + p.Dribbling * .18 + p.Physical * .15 + p.Passing * .1 + p.Defending * .05
        };
        p.Overall = Math.Clamp((int)Math.Round(score), 40, p.Potential);
    }
    private static int Cap(int value) => Math.Clamp(value, 1, 99);
    private static int Floor(int value) => Math.Clamp(value, 1, 99);
    private static double RiskPenalty(string risk) => risk == "Alto" ? .13 : risk == "Medio" ? .06 : 0;
    private static double AverageRating(Player p) => p.Appearances == 0 ? 0 : p.RatingTotal / p.Appearances;
    private static double Next(CareerState state) { state.RandomState ^= state.RandomState << 13; state.RandomState ^= state.RandomState >> 17; state.RandomState ^= state.RandomState << 5; return (state.RandomState % 10000) / 10000d; }
    private static int NextInt(CareerState state, int max) => (int)(Next(state) * max);

    private void AddGlobalWorldCatalog()
    {
        AddRegion("Argentina", "Torneos cortos, clasicos y exportacion de talento");
        AddRegion("Brasil", "Liga nacional, copas y mercado sudamericano");
        AddRegion("Colombia", "Torneos cortos, cuadrangulares y cantera exportadora");
        AddRegion("Estados Unidos", "Conferencias, playoffs y reglas de plantilla MLS");
        AddNationalTeam("México", "CONCACAF", 76, 1); AddNationalTeam("Argentina", "CONMEBOL", 92, 3); AddNationalTeam("Brasil", "CONMEBOL", 93, 5); AddNationalTeam("Colombia", "CONMEBOL", 82, 0);
        AddNationalTeam("Estados Unidos", "CONCACAF", 80, 0); AddNationalTeam("España", "UEFA", 90, 1); AddNationalTeam("Inglaterra", "UEFA", 89, 1); AddNationalTeam("Japón", "AFC", 80, 0); AddNationalTeam("Francia", "UEFA", 92, 2); AddNationalTeam("Marruecos", "CAF", 82, 0);
        AddLeague("Primera Division Argentina", "Argentina", 82, 1, 29, "apertura-playoffs", 18_000_000m,
            ["Argentinos Juniors", "Atletico Tucuman", "Banfield", "Barracas Central", "Belgrano", "Boca Juniors", "Central Cordoba", "Defensa y Justicia", "Estudiantes", "Gimnasia LP", "Godoy Cruz", "Huracan", "Independiente", "Instituto", "Lanus", "Newell's Old Boys", "Platense", "Racing Club", "River Plate", "Rosario Central", "San Lorenzo", "Sarmiento", "Talleres", "Tigre", "Union", "Velez Sarsfield"]);
        AddLeague("Primera Nacional Argentina", "Argentina", 61, 2, 34, "zones-playoffs", 1_500_000m,
            ["All Boys", "Almagro", "Atlanta", "Chacarita Juniors", "Colon", "Deportivo Madryn", "Estudiantes BA", "Ferro Carril Oeste", "Gimnasia Mendoza", "Nueva Chicago", "Quilmes", "San Martin SJ", "San Martin Tucuman", "Temperley", "Tigre B", "Tristan Suarez"]);
        AddLeague("Brasileirao Serie A", "Brasil", 88, 1, 38, "league", 30_000_000m,
            ["Atletico Mineiro", "Bahia", "Botafogo", "Corinthians", "Cruzeiro", "Flamengo", "Fluminense", "Fortaleza", "Gremio", "Internacional", "Juventude", "Mirassol", "Palmeiras", "RB Bragantino", "Santos", "Sao Paulo", "Sport Recife", "Vasco da Gama", "Vitoria", "Ceara"]);
        AddLeague("Brasileirao Serie B", "Brasil", 67, 2, 38, "league-playoffs", 4_000_000m,
            ["America Mineiro", "Athletico Paranaense", "Avai", "Chapecoense", "Coritiba", "Criciuma", "Goias", "Guarani", "Operario", "Ponte Preta", "Remo", "Vila Nova", "Volta Redonda", "Paysandu", "CRB", "Amazonas", "Novorizontino", "Cuiaba", "Atletico Goianiense", "Botafogo SP"]);
        AddLeague("Liga BetPlay Colombia", "Colombia", 72, 1, 19, "apertura-playoffs", 5_000_000m,
            ["America de Cali", "Atletico Bucaramanga", "Atletico Nacional", "Boyaca Chico", "Deportivo Cali", "Deportes Tolima", "Envigado", "Fortaleza CEIF", "Independiente Medellin", "Independiente Santa Fe", "Junior", "La Equidad", "Millonarios", "Once Caldas", "Pasto", "Pereira", "Union Magdalena", "Alianza", "Jaguares", "Llaneros"]);
        AddLeague("Torneo BetPlay Colombia", "Colombia", 53, 2, 16, "apertura-playoffs", 750_000m,
            ["Barranquilla FC", "Boca Juniors de Cali", "Cucuta Deportivo", "Deportes Quindio", "Leones", "Orsomarso", "Patriotas", "Real Cartagena", "Real Santander", "Tigres FC", "Union Magdalena B", "Valledupar", "Bogota FC", "Internacional de Palmira", "Llaneros B", "Atletico Huila"]);
        AddLeague("Major League Soccer", "Estados Unidos", 80, 1, 34, "mls-playoffs", 18_000_000m,
            ["Atlanta United", "Austin FC", "CF Montreal", "Charlotte FC", "Chicago Fire", "Colorado Rapids", "Columbus Crew", "DC United", "FC Cincinnati", "FC Dallas", "Houston Dynamo", "Inter Miami", "LA Galaxy", "LAFC", "Minnesota United", "Nashville SC", "New England Revolution", "New York City FC", "New York Red Bulls", "Orlando City", "Philadelphia Union", "Portland Timbers", "Real Salt Lake", "San Diego FC", "San Jose Earthquakes", "Seattle Sounders", "Sporting Kansas City", "St. Louis City", "Toronto FC", "Vancouver Whitecaps"]);
        AddCompetition("Copa Argentina", "Argentina", "Copa"); AddCompetition("Copa do Brasil", "Brasil", "Copa"); AddCompetition("Copa Colombia", "Colombia", "Copa"); AddCompetition("US Open Cup", "Estados Unidos", "Copa");
        AddCompetition("Copa Libertadores", "CONMEBOL", "Continental"); AddCompetition("Copa Sudamericana", "CONMEBOL", "Continental"); AddCompetition("Concacaf Champions Cup", "CONCACAF", "Continental"); AddCompetition("AFC Champions League Elite", "Asia", "Continental");

        foreach (var league in _world.Leagues)
        {
            if (string.IsNullOrWhiteSpace(league.FormatKey) || league.FormatKey == "league") league.FormatKey = FormatKey(league.Name);
            if (league.MarketScaleEur <= 1_000_000m) league.MarketScaleEur = MarketScale(league.Name);
        }
        foreach (var club in _world.Clubs)
        {
            var titles = HistoricTitles(club.Name);
            club.HistoricalTitles = Math.Max(club.HistoricalTitles, titles);
            club.RecentStrength = Math.Max(club.RecentStrength, RecentStrength(club.Name, club.Prestige));
            club.FinancialTier = Math.Max(club.FinancialTier, FinancialTier(club.Name, club.League));
            club.RecruitmentProfile = RecruitmentProfile(club.League, club.Name);
            club.Prestige = ClubBaselineStrength(titles, club.RecentStrength, club.FinancialTier);
        }
    }

    private void AddRegion(string name, string style)
    {
        if (_world.Regions.All(x => x.Name != name)) _world.Regions.Add(new Region { Name = name, Style = style });
    }

    private void AddNationalTeam(string country, string confederation, int strength, int titles)
    {
        if (_world.NationalTeams.All(x => x.Country != country)) _world.NationalTeams.Add(new NationalTeamProfile { Country = country, Confederation = confederation, Strength = strength, HistoricalTitles = titles });
    }

    private NationalTeamProfile NationalTeamFor(string country) => _world.NationalTeams.FirstOrDefault(x => x.Country == country)
        ?? new NationalTeamProfile { Country = country, Confederation = "Internacional", Strength = 65, HistoricalTitles = 0 };

    private void AddCompetition(string name, string scope, string type)
    {
        if (_world.Competitions.All(x => x.Name != name)) _world.Competitions.Add(new Competition { Name = name, Scope = scope, Type = type });
    }

    private void AddLeague(string name, string region, int prestige, int tier, int matches, string format, decimal marketScale, string[] clubs)
    {
        if (_world.Leagues.Any(x => x.Name == name)) return;
        var promotionLeague = tier == 2 ? _world.Leagues.FirstOrDefault(x => x.Region == region && x.Tier == 1)?.Name : null;
        var relegationLeague = tier == 1 ? _world.Leagues.FirstOrDefault(x => x.Region == region && x.Tier == 2)?.Name : null;
        var league = new League { Name = name, Region = region, Prestige = prestige, Tier = tier, MatchesPerTeam = matches, Format = format, FormatKey = format, MarketScaleEur = marketScale, ClubNames = clubs.ToList(), PromotionCount = tier == 2 ? 2 : 0, RelegationCount = tier == 1 && region != "Estados Unidos" ? 2 : 0, PromotionLeague = promotionLeague, RelegationLeague = relegationLeague };
        _world.Leagues.Add(league);
        if (tier == 2)
        {
            var top = _world.Leagues.FirstOrDefault(x => x.Region == region && x.Tier == 1);
            if (top is not null) top.RelegationLeague = name;
        }
        foreach (var clubName in clubs) _world.Clubs.Add(new Club { Name = clubName, League = name, Region = region, Nickname = clubName, Prestige = prestige - 12 });
    }

    private static string FormatKey(string league) => league switch
    {
        "Liga MX" or "Liga de Expansion MX" or "Liga BetPlay Colombia" or "Torneo BetPlay Colombia" => "apertura-playoffs",
        "Major League Soccer" => "mls-playoffs",
        "Primera Division Argentina" => "apertura-playoffs",
        "Primera Nacional Argentina" => "zones-playoffs",
        _ => "league"
    };

    private static decimal MarketScale(string league) => league switch
    {
        "Premier League" => 45_000_000m, "LALIGA EA SPORTS" => 24_000_000m, "Brasileirao Serie A" => 30_000_000m,
        "Major League Soccer" or "Primera Division Argentina" => 18_000_000m, "Liga MX" => 10_000_000m,
        "J1 League" => 2_000_000m, "EFL Championship" or "LALIGA HYPERMOTION" => 7_000_000m, _ => 1_500_000m
    };

    private static int HistoricTitles(string club) => club switch
    {
        "Real Madrid" => 36, "FC Barcelona" => 28, "Liverpool" or "Manchester United" => 20, "Arsenal" => 13,
        "Boca Juniors" => 35, "River Plate" => 38, "Racing Club" => 18, "Independiente" => 16,
        "Kashima Antlers" => 9, "America" or "América" => 16, "Guadalajara" => 12, "Toluca" => 11,
        "Palmeiras" => 12, "Santos" => 8, "Corinthians" => 7, "Atletico Nacional" => 18, "Millonarios" => 16,
        _ => 1
    };

    private static int RecentStrength(string club, int fallback) => club switch
    {
        "Arsenal" or "Liverpool" or "Manchester City" or "Real Madrid" or "FC Barcelona" or "Palmeiras" or "Flamengo" or "River Plate" or "Boca Juniors" => 88,
        "Vissel Kobe" or "Kashima Antlers" or "Club America" or "América" or "Toluca" or "Cruz Azul" or "Inter Miami" => 80,
        _ => Math.Clamp(fallback, 45, 78)
    };

    private static int FinancialTier(string club, string league) => club switch
    {
        "Real Madrid" or "FC Barcelona" or "Manchester City" or "Manchester United" or "Liverpool" or "Chelsea" or "Arsenal" or "Bayern Munich" => 5,
        "River Plate" or "Boca Juniors" or "Palmeiras" or "Flamengo" or "America" or "América" or "Monterrey" or "Tigres UANL" or "Inter Miami" => 4,
        _ => league is "Premier League" or "LALIGA EA SPORTS" or "Brasileirao Serie A" ? 3 : 2
    };

    private static string RecruitmentProfile(string league, string club) => league switch
    {
        "Premier League" => "Internacional", "LALIGA EA SPORTS" => "Cantera y cesiones", "Liga MX" => "Regional", "J1 League" => "Domestico", "Major League Soccer" => "Reglas MLS",
        "Primera Division Argentina" or "Liga BetPlay Colombia" => "Exportador", "Brasileirao Serie A" => "Talento sudamericano", _ => "Equilibrado"
    };

    private static int ClubBaselineStrength(int titles, int recentStrength, int financialTier) => Math.Clamp((int)Math.Round(Math.Min(100, titles * 2.5) * .45 + recentStrength * .35 + financialTier * 20 * .2), 35, 96);

    private List<ClubWorldProfile> BuildWorldProfiles(CareerState state) => _world.Clubs.Select(club => new ClubWorldProfile
    {
        Club = club.Name, League = club.League, HistoricalTitles = club.HistoricalTitles, RecentStrength = club.RecentStrength,
        FinancialTier = club.FinancialTier, SquadStrength = ClubBaselineStrength(club.HistoricalTitles, club.RecentStrength, club.FinancialTier),
        RecruitmentProfile = club.RecruitmentProfile, TransferBudgetEur = Math.Round(MarketScale(club.League) * (.25m + club.FinancialTier * .18m))
    }).ToList();

    private void SimulateWorldSeason(CareerState state, int playerPosition, List<string> playerTitles)
    {
        if (state.WorldClubs.Count == 0) state.WorldClubs = BuildWorldProfiles(state);
        var playerLeague = state.CurrentLeague;
        foreach (var league in _world.Leagues)
        {
            var clubs = state.WorldClubs.Where(x => x.League == league.Name).ToList();
            if (clubs.Count < 2) continue;
            List<TableRow> table;
            string champion;
            if (league.Name == playerLeague)
            {
                table = state.Table.Select(x => new TableRow { Club = x.Club, Played = x.Played, Points = x.Points, GoalDifference = x.GoalDifference, GoalsFor = x.GoalsFor, GoalsAgainst = x.GoalsAgainst, Wins = x.Wins, Draws = x.Draws, Losses = x.Losses }).ToList();
                champion = table.FirstOrDefault()?.Club ?? state.CurrentClub;
            }
            else
            {
                table = SimulateWorldTable(state, clubs, league);
                champion = ResolveCompetitionChampion(state, league, table);
            }
            var record = new WorldSeasonRecord { Season = state.Season, Competition = league.Name, Champion = champion, RunnerUp = table.Skip(1).FirstOrDefault()?.Club, Table = table };
            ApplyWorldMovement(state, league, table, record);
            state.WorldHistory.Add(record);
        }
        SimulateWorldTransfers(state);
        SimulateWorldCups(state);
        var latest = state.WorldHistory.Where(x => x.Season == state.Season).Select(x => $"{x.Competition}: {x.Champion}").Take(4);
        state.Timeline.Add($"Mundo {state.Season}: {string.Join(" · ", latest)}.");
    }

    private void SimulateWorldCups(CareerState state)
    {
        foreach (var competition in _world.Competitions)
        {
            var candidates = competition.Scope switch
            {
                "Europa" => state.WorldClubs.Where(x => x.League is "Premier League" or "LALIGA EA SPORTS").ToList(),
                "CONMEBOL" => state.WorldClubs.Where(x => x.League.Contains("Argentina") || x.League.Contains("Brasil") || x.League.Contains("Colombia")).ToList(),
                "CONCACAF" => state.WorldClubs.Where(x => x.League is "Liga MX" or "Major League Soccer").ToList(),
                "Asia" => state.WorldClubs.Where(x => x.League == "J1 League").ToList(),
                "Mundial" => state.WorldClubs.Where(x => x.FinancialTier >= 4).ToList(),
                _ => state.WorldClubs.Where(x => RegionForLeague(x.League) == competition.Scope).ToList()
            };
            if (candidates.Count < 2) continue;
            var finalists = candidates.OrderByDescending(x => x.SquadStrength + NextInt(state, 22)).Take(Math.Min(8, candidates.Count)).OrderBy(_ => Next(state)).Take(2).ToList();
            var champion = finalists.OrderByDescending(x => x.SquadStrength + NextInt(state, 20)).First();
            var runnerUp = finalists.First(x => x.Club != champion.Club);
            state.WorldHistory.Add(new WorldSeasonRecord { Season = state.Season, Competition = competition.Name, Champion = champion.Club, RunnerUp = runnerUp.Club });
        }
    }

    private string RegionForLeague(string league) => _world.Leagues.FirstOrDefault(x => x.Name == league)?.Region ?? "";

    private List<TableRow> SimulateWorldTable(CareerState state, List<ClubWorldProfile> clubs, League league) => clubs
        .Select(club =>
        {
            var volatility = NextInt(state, 31) - 15;
            var points = Math.Clamp((club.SquadStrength + volatility) * league.MatchesPerTeam / 100 + NextInt(state, 15), 12, league.MatchesPerTeam * 3);
            return new TableRow { Club = club.Club, Played = league.MatchesPerTeam, Points = points, GoalDifference = NextInt(state, 45) - 12, GoalsFor = 20 + NextInt(state, 55), GoalsAgainst = 18 + NextInt(state, 45) };
        }).OrderByDescending(x => x.Points).ThenByDescending(x => x.GoalDifference).ThenByDescending(x => x.GoalsFor).ToList();

    private string ResolveCompetitionChampion(CareerState state, League league, List<TableRow> table)
    {
        if (league.FormatKey is "apertura-playoffs" or "zones-playoffs" or "mls-playoffs")
        {
            var finalists = table.Take(Math.Min(8, table.Count)).OrderBy(_ => Next(state)).Take(2).ToList();
            return finalists.OrderByDescending(x => WorldStrength(state, x.Club) + NextInt(state, 18)).First().Club;
        }
        return table[0].Club;
    }

    private int WorldStrength(CareerState state, string club) => state.WorldClubs.FirstOrDefault(x => x.Club == club)?.SquadStrength ?? Club(club)?.Prestige ?? 50;

    private void ApplyWorldMovement(CareerState state, League league, List<TableRow> table, WorldSeasonRecord record)
    {
        if (league.RelegationCount <= 0 || string.IsNullOrWhiteSpace(league.RelegationLeague)) return;
        var down = table.TakeLast(Math.Min(league.RelegationCount, table.Count)).Select(x => x.Club).ToList();
        var lower = _world.Leagues.FirstOrDefault(x => x.Name == league.RelegationLeague);
        if (lower is null) return;
        var up = state.WorldClubs.Where(x => x.League == lower.Name).OrderByDescending(x => x.SquadStrength + NextInt(state, 12)).Take(Math.Min(league.RelegationCount, down.Count)).Select(x => x.Club).ToList();
        foreach (var club in down) state.WorldClubs.First(x => x.Club == club).League = lower.Name;
        foreach (var club in up) state.WorldClubs.First(x => x.Club == club).League = league.Name;
        record.Relegated = down; record.Promoted = up;
    }

    private void SimulateWorldTransfers(CareerState state)
    {
        var positions = new[] { "Portero", "Defensa", "Mediocampista", "Extremo", "Delantero" };
        foreach (var league in _world.Leagues)
        {
            var buyers = state.WorldClubs.Where(x => x.League == league.Name).OrderBy(_ => Next(state)).Take(Math.Min(3, league.ClubNames.Count)).ToList();
            foreach (var buyer in buyers)
            {
                var sellers = state.WorldClubs.Where(x => x.Club != buyer.Club && x.SquadStrength >= buyer.SquadStrength - 8).OrderBy(_ => Next(state)).Take(1).ToList();
                if (sellers.Count == 0) continue;
                var seller = sellers[0]; var type = Next(state) < .35 ? "Cesion" : Next(state) < .57 ? "Agente libre" : "Traspaso";
                var fee = type == "Agente libre" ? 0m : Math.Round(Math.Min(buyer.TransferBudgetEur * .28m, MarketScale(buyer.League) * (.03m + (decimal)Next(state) * .12m)) / 50_000m) * 50_000m;
                state.WorldTransfers.Add(new WorldTransfer { Season = state.Season, PlayerName = $"{ScoutName(state)} {ScoutSurname(state)}", Position = positions[NextInt(state, positions.Length)], FromClub = type == "Agente libre" ? "Sin club" : seller.Club, ToClub = buyer.Club, FeeEur = fee, Type = type });
                buyer.SquadStrength = Math.Min(94, buyer.SquadStrength + 1); seller.SquadStrength = Math.Max(38, seller.SquadStrength - (type == "Traspaso" ? 1 : 0)); buyer.TransferBudgetEur = Math.Max(0, buyer.TransferBudgetEur - fee);
            }
        }
    }

    private static string ScoutName(CareerState state) => new[] { "Mateo", "Luca", "Thiago", "Noah", "Diego", "Santiago", "Hiro", "Kai" }[NextInt(state, 8)];
    private static string ScoutSurname(CareerState state) => new[] { "Silva", "Morales", "Tanaka", "Santos", "Rios", "Costa", "Garcia", "Lopez" }[NextInt(state, 8)];

    private void AddExpandedEventCatalog()
    {
        if (_events.Templates.Count >= 250) return;
        var extras = new (string Trigger, string Title, string Game, string Rarity, int Age)[]
        {
            ("life","Cena con tu pareja","Memorama de conversaciones","common",16),("life","Ruptura antes del clasico","Decisión de calma","rare",16),("life","Noche con el vestuario","Dado de disciplina","common",18),("life","Invitacion a un bar","Ruta de dados","common",18),("life","Rumor en redes","Cartas de reputación","common",16),("life","Apoyo familiar","Memorama de prioridades","common",16),("life","Fiesta tras la victoria","Dado de recuperación","rare",18),("life","Conflicto con un amigo","Decisión de confianza","rare",16),("life","Nueva vivienda","Tablero de presupuesto","common",18),("life","Aniversario en semana clave","Memorama de agenda","rare",16),
            ("board","La directiva mejora las canchas","Mapa táctico","common",16),("board","Reunion sobre instalaciones","Tablero de proyecto","common",16),("board","El club pide embajador","Cartas de imagen","common",16),("board","Cambio de entrenador","Memorama táctico","rare",16),("board","Capitania disponible","Dado de liderazgo","rare",18),("board","Viaje de pretemporada","Ruta de dados","common",16),("board","Crisis en la directiva","Tablero de confianza","superrare",16),("board","Nuevo centro medico","Buscaminas de recuperación","rare",16),
            ("finance","Patrocinio local","Negociación","common",18),("finance","Inversion inmobiliaria ficticia","Tablero de presupuesto","rare",18),("finance","Asesor financiero","Cartas de riesgo","common",18),("finance","Casino ficticio","Dados de suerte","rare",18),("finance","Multa por retraso","Decisión de responsabilidad","common",16),("finance","Marca internacional","Negociación","superrare",18),
            ("integrity","Propuesta de dejarse ganar","Decisión de integridad","superrare",18),("integrity","Apuesta sospechosa","Cartas de integridad","rare",18),("integrity","Filtracion del vestuario","Memorama de confianza","rare",16),("integrity","Presion de un intermediario","Decisión de integridad","superrare",18),("integrity","Regalo inapropiado","Decisión de integridad","rare",18),("integrity","Investigacion del club","Tablero de reputación","superrare",18)
        };
        foreach (var item in extras.Take(90 - _events.Templates.Count).Select((x, index) => new { x, index }))
            _events.Templates.Add(new EventTemplate { Id = $"extra-{item.index + 1:00}", Trigger = item.x.Trigger, Title = item.x.Title, MiniGame = item.x.Game, Rarity = item.x.Rarity, MinAge = item.x.Age });
        AddCatalogBatch("match", "Partido", "sport", ["Remate tras centro", "Contraataque en inferioridad", "Penal bajo presión", "Tiro libre decisivo", "Último pase entre líneas", "Balón dividido", "Clásico con tensión", "Final de copa", "Debut de un canterano", "Racha sin ganar", "Gol anulado", "Expulsión de un compañero", "Remontada en casa", "Partido bajo lluvia", "Rivalidad regional", "Minutos de descuento", "Lesión de tu socio", "Cambio táctico urgente", "Defensa de resultado", "Objetivo de salvación"], ["lectura de juego"]);
        AddCatalogBatch("recovery", "Recuperación", "recovery", ["Control de cargas", "Protocolo de sueño", "Sesión de fisioterapia", "Chequeo de rodilla", "Molestia de tobillo", "Fatiga mental", "Sobrecarga de gemelo", "Dolor lumbar", "Prevención de recaída", "Viaje largo", "Golpe de hombro", "Césped pesado", "Semana de tres partidos", "Nutrición deportiva", "Trabajo de movilidad", "Frío postpartido", "Recuperación activa", "Análisis de fatiga", "Consulta con especialista", "Regreso progresivo"], ["plan conservador", "plan exigente"]);
        AddCatalogBatch("board", "Club", "club", ["Charla privada con el técnico", "Competencia por titularidad", "Nuevo sistema táctico", "Cambio de capitán", "Reunión con la directiva", "Obras en el estadio", "Gira internacional", "Promesa de minutos", "Jugador veterano te aconseja", "Conflicto de vestuario", "Cambio de preparador físico", "Objetivo de clasificación", "Revisión de cláusula", "Agente pide reunión"], ["acuerdo", "tensión", "oportunidad"]);
        AddCatalogBatch("life", "Vida personal", "life", ["Cena familiar", "Amistad en problemas", "Mudanza a otra ciudad", "Relación a distancia", "Rumor sentimental", "Invitación a evento", "Mensaje de un ídolo", "Visita a tu barrio", "Cumpleaños en concentración", "Día libre inesperado", "Nueva mascota", "Entrevista sobre tu vida", "Amigo pide ayuda", "Discusión de pareja", "Vacaciones cortas", "Celebración privada", "Redes sociales intensas", "Apoyo psicológico", "Reencuentro escolar", "Aficionado insistente"], ["decisión serena", "decisión ambiciosa"]);
        AddCatalogBatch("finance", "Finanzas", "finance", ["Patrocinio de botas", "Asesor propone ahorro", "Donación benéfica", "Compra importante"], ["oferta prudente"]);
        AddCatalogBatch("integrity", "Integridad", "integrity", ["Dato filtrado a prensa", "Intermediario sospechoso", "Regalo de un empresario", "Presión de apuestas", "Compañero bajo investigación", "Oferta de información", "Llamada anónima"], ["rechazar y reportar", "aceptar el riesgo"]);
        NormalizeCatalog();
    }

    private void AddCatalogBatch(string trigger, string category, string group, string[] topics, string[] variants)
    {
        foreach (var topic in topics)
        foreach (var variant in variants)
        {
            var number = _events.Templates.Count + 1;
            _events.Templates.Add(new EventTemplate
            {
                Id = $"{group}-{number:000}", Trigger = trigger, Category = category, Family = $"{group}:{topic}",
                Title = $"{topic} · {variant}", Description = $"{topic}. La decisión afectará tu carrera de forma persistente.",
                MiniGame = "Decisión contextual", MinAge = group is "finance" or "integrity" ? 18 : 16,
                SafeOption = group == "integrity" ? "Rechazar y reportar" : "Elegir la opción responsable",
                RiskOption = group == "integrity" ? "Aceptar el riesgo" : "Buscar una ventaja inmediata",
                EffectProfile = group
            });
        }
    }

    private void NormalizeCatalog()
    {
        foreach (var template in _events.Templates)
        {
            if (string.IsNullOrWhiteSpace(template.Category)) template.Category = template.Trigger switch { "training" => "Entrenamiento", "recovery" => "Recuperación", "press" => "Prensa", "board" => "Club", "life" => "Vida personal", "finance" => "Finanzas", "integrity" => "Integridad", _ => "Partido" };
            if (string.IsNullOrWhiteSpace(template.Family)) template.Family = $"{template.Trigger}:{template.Id}";
            if (string.IsNullOrWhiteSpace(template.EffectProfile)) template.EffectProfile = template.Trigger switch { "training" => "training", "recovery" => "recovery", "press" => "press", "board" => "club", _ => template.Trigger };
        }
        var ordered = _events.Templates.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].Rarity = i < 170 ? "common" : i < 233 ? "rare" : "superrare";
    }

    private static readonly string[] SeasonMiniGames = ["penalty", "freekick", "finish", "pass", "interception", "save", "aerial", "rondo", "mines", "tictactoe", "targets", "focus", "casino-dice", "roulette", "blackjack"];
    private static bool IsMatchMiniGame(string id) => id is "penalty" or "freekick" or "finish" or "pass" or "interception" or "save" or "aerial";
    private static string MiniGameName(string id) => id switch
    {
        "penalty" => "Penal a las esquinas", "freekick" => "Tiro libre", "finish" => "Definición mano a mano", "pass" => "Último pase",
        "interception" => "Intercepción", "save" => "Parada del portero", "aerial" => "Duelo aéreo", "rondo" => "Rondo de memoria",
        "mines" => "Buscaminas de recuperación", "tictactoe" => "Tres en raya táctico", "targets" => "Tres objetivos", "focus" => "Concentración de reflejos", "casino-dice" => "Dados de casino", "roulette" => "Ruleta ficticia", "blackjack" => "Blackjack ficticio", _ => "Desafío"
    };

    private string SelectSeasonMiniGame(CareerState state)
    {
        var roll = Next(state);
        if (state.Player.Age >= 18 && roll >= .90) return SeasonMiniGames[12 + NextInt(state, 3)];
        if (roll >= .70) return SeasonMiniGames[7 + NextInt(state, 5)];
        var matchGames = SeasonMiniGames.Take(7).ToList();
        return matchGames[NextInt(state, matchGames.Count)];
    }

    private MiniGameChallenge CreateChallenge(CareerState state, string gameId)
    {
        var sequenceLength = gameId is "pass" or "aerial" or "rondo" or "focus" ? 3 + NextInt(state, 2) : gameId is "targets" ? 3 : 1;
        var boardSize = gameId is "pass" or "interception" or "save" or "aerial" or "rondo" or "mines" or "tictactoe" or "targets" or "focus" ? 9 : 100;
        var targets = Enumerable.Range(0, sequenceLength).Select(_ => NextInt(state, boardSize)).ToList();
        var relevant = gameId switch { "save" => state.Player.Goalkeeping, "interception" => state.Player.Defending, "pass" or "rondo" => state.Player.Passing, "aerial" => state.Player.Physical, _ => state.Player.Shooting };
        var difficulty = Math.Clamp(58 + (Club(state.CurrentClub)?.Prestige ?? 50) / 5 - relevant / 10 + (100 - state.Player.Energy) / 8, 48, 82);
        var mode = gameId switch { "penalty" or "freekick" or "finish" => "shoot", "save" => "keeper", "interception" => "lanes", "mines" => "mines", "tictactoe" => "tictactoe", "targets" => "targets", "casino-dice" => "dice", "roulette" => "roulette", "blackjack" => "blackjack", _ => "memory" };
        return new MiniGameChallenge
        {
            GameId = gameId, Mode = mode, TargetSequence = targets, SafeTiles = gameId == "mines" ? targets.Distinct().ToList() : [], TargetLane = NextInt(state, 3), BoardSize = boardSize, RequiredScore = difficulty, DealerScore = 15 + NextInt(state, 7), PlayerScore = 12 + NextInt(state, 6), Choices = mode switch { "dice" => ["Bajo", "Alto"], "roulette" => ["Rojo", "Negro", "Verde"], "blackjack" => ["Plantarse", "Pedir", "Doblar"], _ => [] },
            IsLuckGame = mode is "dice" or "roulette" or "blackjack",
            Instructions = gameId switch
            {
                "penalty" => "Detén el marcador dentro de la zona verde.", "freekick" => "Encuentra el golpeo limpio en la zona verde.", "finish" => "Mide el toque final con precisión.",
                "pass" => "Memoriza y pulsa la ruta de pase iluminada.", "interception" => "Lee la trayectoria y pulsa los carriles correctos.", "save" => "Elige la secuencia de paradas correcta.",
                "aerial" => "Completa salto, orientación y contacto.", "rondo" => "Repite la secuencia de pases con falsos apoyos.", "mines" => "Encuentra tres casillas seguras y retírate antes de una mina.", "tictactoe" => "Haz tres en raya antes que la defensa.", "targets" => "Acierta los tres objetivos en el orden correcto.", "focus" => "Memoriza el patrón y evita los distractores.", "roulette" => "Elige color: verde paga más, pero casi nunca sale.", "blackjack" => "Acércate a 21 sin pasarte; el crupier ya tiene cartas.", _ => "Elige alto o bajo; el dado decide la fortuna ficticia."
            }
        };
    }

    private bool ResolveMiniGame(CareerState state, MiniGameChallenge challenge, MiniGameSubmission? submission)
    {
        if (submission is null) return false;
        if (challenge.IsLuckGame)
        {
            var choice = submission.Choice ?? (submission.Moves.FirstOrDefault(-1) == 0 ? "Bajo" : "Alto");
            if (challenge.Mode == "roulette" && string.IsNullOrEmpty(submission.Choice)) choice = submission.Moves.FirstOrDefault(-1) == 0 ? "Rojo" : "Negro";
            if (challenge.Mode == "blackjack" && string.IsNullOrEmpty(submission.Choice)) choice = submission.Moves.FirstOrDefault(-1) == 0 ? "Plantarse" : "Pedir";
            if (challenge.Mode == "dice") { var die = 1 + NextInt(state, 6); return choice == (die >= 4 ? "Alto" : "Bajo"); }
            if (challenge.Mode == "roulette") { var roll = NextInt(state, 37); var result = roll == 0 ? "Verde" : roll % 2 == 0 ? "Rojo" : "Negro"; return choice == result; }
            if (challenge.Mode == "blackjack") { var hand = challenge.PlayerScore + (choice == "Pedir" ? 1 + NextInt(state, 10) : choice == "Doblar" ? 2 + NextInt(state, 10) : 0); return hand <= 21 && (challenge.DealerScore > 21 || hand >= challenge.DealerScore); }
            return false;
        }
        if (challenge.Mode is "shoot" or "keeper") return submission.Moves.FirstOrDefault(-1) == challenge.TargetLane && (submission.Score ?? -1) >= challenge.RequiredScore;
        if (challenge.Mode == "mines") return submission.Moves.Count >= 3 && submission.Moves.Take(3).All(challenge.SafeTiles.Contains) && submission.Moves.Take(3).Distinct().Count() == 3;
        if (challenge.Mode == "tictactoe")
        {
            var lines = new[] { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, new[] { 0, 4, 8 }, new[] { 2, 4, 6 } };
            return submission.Moves.Count == 3 && submission.Moves.Distinct().Count() == 3 && submission.Moves.All(x => x is >= 0 and < 9) && lines.Any(line => line.All(submission.Moves.Contains));
        }
        if (challenge.BoardSize == 100)
        {
            var score = submission.Score ?? -1;
            return score is >= 0 and <= 100 && score >= challenge.RequiredScore;
        }
        return submission.Moves.Count == challenge.TargetSequence.Count && submission.Moves.SequenceEqual(challenge.TargetSequence);
    }

    private CareerState ResolveSpecialMiniGame(CareerState state, SeasonEvent active, EventOption option, MiniGameSubmission? submission)
    {
        var success = ResolveMiniGame(state, active.Challenge!, submission);
        if (!active.IsHubActivity) state.SeasonMiniGameUsed = true;
        var casinoAmount = 0;
        if (active.Challenge!.IsLuckGame)
        {
            var amount = Math.Min(Math.Max(100m, state.Player.Money * .03m), 3_000m);
            var multiplier = active.Challenge.Mode == "roulette" && (submission?.Choice == "Verde") ? 12m : active.Challenge.Mode == "blackjack" && submission?.Choice == "Doblar" ? 2m : 1m;
            casinoAmount = (int)Math.Min(success ? amount * multiplier : amount, int.MaxValue);
            AddLedger(state, "Casino ficticio", success ? $"{active.MiniGame}: jugada ganadora" : $"{active.MiniGame}: jugada perdida", success ? amount * multiplier : -amount);
            state.Player.Morale = Floor(state.Player.Morale + (success ? 2 : -3)); state.Player.MediaRelation = Floor(state.Player.MediaRelation + (success ? 0 : -2));
        }
        else if (success) { state.Player.Form = Cap(state.Player.Form + 5); state.Player.Morale = Cap(state.Player.Morale + 3); }
        else { state.Player.Energy = Floor(state.Player.Energy - 6); state.Player.Form = Floor(state.Player.Form - 3); }
        active.Outcome = success ? $"Superaste {active.MiniGame}." : $"Fallaste {active.MiniGame}; la consecuencia se aplicó.";
        active.Resolution = new EventResolution { Headline = active.Title, Result = success ? "Éxito" : "Fallo", Detail = active.Outcome, Effects = [new EventEffect { Label = active.Challenge.IsLuckGame ? "Saldo" : "Forma", Value = active.Challenge.IsLuckGame ? casinoAmount : success ? 5 : -3, Direction = success ? "positive" : "negative" }] };
        state.LastResolution = active.Resolution; state.CompletedEvents.Add(active); state.Timeline.Add(active.Outcome); state.ActiveEvent = null;
        return state;
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
