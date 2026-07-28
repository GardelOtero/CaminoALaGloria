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
    }

    public WorldCatalog World() => _world;

    public CareerState Create(CreateCareerRequest request)
    {
        var club = _world.Clubs.FirstOrDefault(c => c.Name == request.Club && c.League == request.League)
            ?? _world.Clubs.FirstOrDefault(c => c.League == request.League) ?? _world.Clubs.First();
        var state = new CareerState
        {
            RandomState = (uint)HashCode.Combine(request.Name, request.Position, DateTime.UtcNow.Ticks),
            CurrentClub = club.Name, CurrentLeague = club.League,
            Player = new Player
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Promesa" : request.Name.Trim(),
                Nationality = _world.Nationalities.Contains(request.Nationality) ? request.Nationality : _world.Nationalities.First(),
                ShirtNumber = Math.Clamp(request.ShirtNumber, 1, 99), Position = request.Position,
                Archetype = request.Archetype, Personality = request.Personality,
                Overall = StartingOverall(request.Position), Potential = 80 + (request.Archetype == "Talento" ? 6 : 0)
            }
        };
        InitializeAttributes(state.Player, state);
        InitializeSeason(state);
        state.Contract = CreateContract(state, club, "Cantera");
        AddLedger(state, "Firma", $"Bono de firma con {club.Name}", state.Contract.SigningBonusEur);
        state.Timeline.Add($"Firmaste tu primer contrato con {club.Name} en {club.League}.");
        state.ActiveEvent = CreatePreseasonEvent(state);
        return state;
    }

    public CareerState AdvanceToNextEvent(CareerState state)
    {
        if (state.IsRetired) throw new InvalidOperationException("La carrera ya finalizo con el retiro del jugador.");
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
            : Next(state) < Math.Clamp(.28 + state.Player.Overall / 220d + state.Player.Form / 320d + state.Player.Energy / 500d - RiskPenalty(option.Risk), .12, .92);
        var fixture = state.Fixtures.First(f => f.Id == active.Match.FixtureId);
        ResolvePlayerFixture(state, fixture, active, success);
        active.Outcome = DescribeMatchOutcome(state, active, option, success, fixture);
        active.Resolution = MatchResolution(state, active, option, success, fixture);
        state.LastResolution = active.Resolution;
        if (active.Challenge is not null) state.SeasonMiniGameUsed = true;
        state.CompletedEvents.Add(active); state.Timeline.Add(active.Outcome); state.EventIndex++;
        state.ActiveEvent = null; UpdateTable(state);

        if (success && IsTransferWindow(state) && state.Player.Reputation >= 15)
        {
            state.TransferOffers = GenerateTransferOffers(state);
            if (state.TransferOffers.Count > 0) state.ActiveEvent = TransferEvent(state);
        }
        if (state.ActiveEvent is null) state.ActiveEvent = CreateOffPitchEvent(state, active, success);
        return state;
    }

    public CareerState CompleteSeason(CareerState state)
    {
        if (!state.SeasonComplete || state.ActiveEvent is not null) throw new InvalidOperationException("Termina los partidos y eventos pendientes antes de cerrar la temporada.");
        var position = state.Table.FindIndex(x => x.Club == state.CurrentClub) + 1;
        var seasonTitles = new List<string>();
        if (position == 1)
        {
            var trophy = $"{state.CurrentLeague} {state.Season}";
            state.Trophies.Add(trophy);
            seasonTitles.Add(trophy);
            state.Timeline.Add($"¡Campeones! {state.CurrentClub} gana {trophy}.");
            if (state.Contract is not null) AddLedger(state, "Prima", "Prima por título", state.Contract.TitleBonusEur);
        }
        ApplyLeagueMovement(state, position);
        var p = state.Player;
        p.Age++; p.Overall = Math.Min(p.Potential, p.Overall + 1 + NextInt(state, 3));
        SaveLastSeasonPerformance(state);
        state.SeasonArchives.Add(new SeasonArchive
        {
            Summary = new SeasonSummary { Season = state.Season, Club = state.CurrentClub, League = state.CurrentLeague, Appearances = p.LastSeasonAppearances, Goals = p.LastSeasonGoals, Average = p.LastSeasonAverage, Titles = seasonTitles, FinalPosition = position },
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
        state.Table = BuildTable(state);
        state.SeasonEventTarget = 3 + NextInt(state, 3);
        state.SeasonMiniGameId = SelectSeasonMiniGame(state);
        state.SeasonMiniGameUsed = false;
        state.SalaryMonthsPaid = 0;
        state.ImportantMatchdays = SelectImportantMatchdays(state);
        state.SeasonStartAppearances = state.Player.Appearances;
        state.SeasonStartGoals = state.Player.Goals;
        state.SeasonStartAssists = state.Player.Assists;
        state.SeasonStartRatingTotal = state.Player.RatingTotal;
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
        var stakes = decisive ? "Partido decisivo: este resultado puede definir título, clasificación o descenso." : StateOfMatch(state, rival);
        var isDefender = state.Player.Position is "Portero" or "Defensa";
        if (decisive) { teamGoals = isDefender ? 1 : 0; rivalGoals = isDefender ? 1 : 0; }
        var options = isDefender
            ? new[] { ("press", "Salir a presionar y recuperar", "Alto"), ("hold", "Mantener la línea y proteger el área", "Bajo") }
            : new[] { ("attack", "Asumir la jugada decisiva", "Alto"), ("combine", "Asociarte y buscar el espacio", "Medio") };
        return new SeasonEvent
        {
            Id = $"match-{fixture.Id}", Category = "Partido", MiniGame = decisive ? (isDefender ? "Última intervención" : "Definición decisiva") : (isDefender ? "Anticipación" : "Decisión ofensiva"),
            Title = $"{state.CurrentClub} vs {rival}", Description = $"Jornada {state.CurrentMatchday}, minuto {minute}. {stakes}",
            Match = new MatchContext { FixtureId = fixture.Id, Matchday = state.CurrentMatchday, Rival = rival, IsHome = home, Minute = minute, TeamGoals = teamGoals, RivalGoals = rivalGoals, Stakes = stakes, IsDecisive = decisive },
            Options = options.Select(x => new EventOption { Id = x.Item1, Label = x.Item2, Risk = x.Item3 }).ToList(),
            Challenge = decisive && !state.SeasonMiniGameUsed && IsMatchMiniGame(state.SeasonMiniGameId) ? CreateChallenge(state, state.SeasonMiniGameId) : null
        };
    }

    private void ResolveBackgroundFixture(CareerState state, MatchFixture fixture)
    {
        var homeStrength = (Club(fixture.Home)?.Prestige ?? 50) + 5; var awayStrength = Club(fixture.Away)?.Prestige ?? 50;
        fixture.HomeGoals = Goals(state, homeStrength, awayStrength); fixture.AwayGoals = Goals(state, awayStrength, homeStrength); fixture.IsPlayed = true;
    }

    private void ResolvePlayerFixture(CareerState state, MatchFixture fixture, SeasonEvent? active, bool success)
    {
        var p = state.Player; var home = fixture.Home == state.CurrentClub;
        var ownStrength = (Club(state.CurrentClub)?.Prestige ?? 50) + p.Overall / 7 + (home ? 5 : 0);
        var rival = home ? fixture.Away : fixture.Home; var rivalStrength = Club(rival)?.Prestige ?? 50;
        var ownGoals = active?.Match?.TeamGoals ?? Goals(state, ownStrength, rivalStrength);
        var rivalGoals = active?.Match?.RivalGoals ?? Goals(state, rivalStrength, ownStrength);
        if (active is not null)
        {
            var defensiveAction = p.Position is "Portero" or "Defensa";
            if (success) { if (!defensiveAction) ownGoals++; p.Form = Math.Min(95, p.Form + 7); p.Reputation += 4; p.Morale = Math.Min(95, p.Morale + 6); }
            else { rivalGoals++; p.Form = Math.Max(30, p.Form - 5); p.Energy = Math.Max(25, p.Energy - 9); p.Morale = Math.Max(25, p.Morale - 5); }
        }
        fixture.HomeGoals = home ? ownGoals : rivalGoals; fixture.AwayGoals = home ? rivalGoals : ownGoals; fixture.IsPlayed = true;
        var rating = Math.Round(5.7 + (p.Overall - 50) / 17d + (success ? .9 : Next(state) - .55), 2);
        p.Appearances++; p.RatingTotal += rating; p.Energy = Math.Max(25, p.Energy - 3);
        if (success && p.Position is "Delantero" or "Extremo") { p.Goals++; if (state.Contract is not null) AddLedger(state, "Prima", "Prima por gol", state.Contract.GoalOrAssistBonusEur); }
        else if (success && p.Position == "Mediocampista") { p.Assists++; if (state.Contract is not null) AddLedger(state, "Prima", "Prima por asistencia", state.Contract.GoalOrAssistBonusEur); }
        if (state.Contract is not null) AddLedger(state, "Prima", "Prima por aparición", state.Contract.AppearanceBonusEur);
    }

    private SeasonEvent CreateOffPitchEvent(CareerState state, SeasonEvent matchEvent, bool matchSuccess)
    {
        var match = matchEvent.Match!;
        if (!state.SeasonMiniGameUsed && !IsMatchMiniGame(state.SeasonMiniGameId))
        {
            var game = state.SeasonMiniGameId;
            var category = game == "casino" ? "Ocio" : "Entrenamiento";
            return new SeasonEvent
            {
                Id = $"minigame-{state.Season}-{game}", Category = category, Title = MiniGameName(game), MiniGame = MiniGameName(game),
                Description = game == "casino" ? "Una noche de ocio ficticio: elige alto o bajo y acepta el resultado." : "Una sesión breve antes de volver al calendario. Completa el desafío para obtener el beneficio.",
                Challenge = CreateChallenge(state, game), Options = [new EventOption { Id = "play", Label = "Resolver desafío", Risk = game == "casino" ? "Suerte" : "Habilidad" }]
            };
        }
        if (Next(state) < .28)
        {
            var trigger = Next(state) < .70 ? "life" : Next(state) < .82 ? "board" : Next(state) < .94 ? "finance" : "integrity";
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
        return new SeasonEvent { Id = training.Id, Category = "Entrenamiento", Title = training.Title, MiniGame = training.MiniGame,
            Description = $"Tu actuación ante {match.Rival} te dejó crédito con el entrenador. Elige una rutina: dos son favorables y una tiene riesgo de retroceso.", Options = CreateTrainingPlanOptions(state), RequiredSelections = 1 };
    }

    private EventTemplate EventTemplate(CareerState state, string trigger)
    {
        var templates = _events.Templates.Where(template => template.Trigger == trigger && template.MinAge <= state.Player.Age).ToList();
        return templates.Count == 0 ? new EventTemplate { Id = trigger, Trigger = trigger, Title = trigger, MiniGame = "Decisión" } : templates[NextInt(state, templates.Count)];
    }

    private static SeasonEvent OffPitch(string id, string category, string title, string description, string minigame, params (string Id, string Label, string Risk)[] options) => new()
    {
        Id = id, Category = category, Title = title, Description = description, MiniGame = minigame,
        Options = options.Select(x => new EventOption { Id = x.Id, Label = x.Label, Risk = x.Risk }).ToList()
    };

    private CareerState ResolveOffPitchEvent(CareerState state, SeasonEvent active, EventOption option)
    {
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
        state.LastResolution = active.Resolution; state.CompletedEvents.Add(active); state.Timeline.Add(outcome); state.ActiveEvent = null;
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

    private static void SaveLastSeasonPerformance(CareerState state)
    {
        var p = state.Player;
        p.LastSeasonAppearances = p.Appearances - state.SeasonStartAppearances;
        p.LastSeasonGoals = p.Goals - state.SeasonStartGoals;
        p.LastSeasonAssists = p.Assists - state.SeasonStartAssists;
        var ratings = p.RatingTotal - state.SeasonStartRatingTotal;
        p.LastSeasonAverage = p.LastSeasonAppearances == 0 ? 5.8 : ratings / p.LastSeasonAppearances;
    }

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
        foreach (var fixture in state.Fixtures.Where(f => f.IsPlayed && f.HomeGoals.HasValue && f.AwayGoals.HasValue))
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
        var current = Club(state.CurrentClub); var threshold = (current?.Prestige ?? 40) + 4;
        return _world.Clubs.Where(c => c.Name != state.CurrentClub && c.Prestige >= threshold).OrderBy(_ => Next(state)).Take(2).Select(c => new TransferOffer
        {
            Club = c.Name, League = c.League,
            Salary = AnnualSalary(state, c), MonthlyNetEur = Math.Round(AnnualSalary(state, c) * NetRate(c.League) / 12m),
            SigningBonusEur = Math.Round(AnnualSalary(state, c) * .08m), Role = c.Prestige > (current?.Prestige ?? 40) + 18 ? "Competencia alta" : "Rotación"
        }).ToList();
    }

    private Contract CreateContract(CareerState state, Club club, string role)
    {
        var annual = AnnualSalary(state, club);
        return new Contract { Club = club.Name, League = club.League, AnnualGrossEur = annual, MonthlyNetEur = Math.Round(annual * NetRate(club.League) / 12m), SigningBonusEur = Math.Round(annual * .08m), AppearanceBonusEur = Math.Max(150m, Math.Round(annual * .002m)), GoalOrAssistBonusEur = Math.Max(100m, Math.Round(annual * .0015m)), TitleBonusEur = Math.Round(annual * .12m), Role = role, Season = state.Season };
    }

    private decimal AnnualSalary(CareerState state, Club club)
    {
        var league = _world.Leagues.First(x => x.Name == club.League);
        var baseSalary = 8_000m + league.Prestige * league.Prestige * 30m;
        var playerFactor = Math.Max(.35m, (state.Player.Overall - 42) / 42m) * (1m + state.Player.Reputation / 140m);
        var ageFactor = state.Player.Age < 19 ? .55m : state.Player.Age > 33 ? .78m : 1m;
        return Math.Round(Math.Clamp(baseSalary * playerFactor * ageFactor * (club.Prestige / 70m), 8_000m, 18_000_000m) / 500m) * 500m;
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
        var moved = destination.Name != state.CurrentClub;
        state.CurrentClub = destination.Name; state.CurrentLeague = destination.League;
        state.Contract = CreateContract(state, destination, moved ? "Nuevo fichaje" : "Renovado");
        AddLedger(state, "Firma", $"Bono de firma con {destination.Name}", state.Contract.SigningBonusEur);
        active.Outcome = moved ? $"Firmaste por {destination.Name}." : $"Renovaste con {destination.Name}.";
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
        return new EventResolution { Headline = active.Title, Result = result, Score = score, Detail = success ? $"{option.Label} cambió la jugada a tu favor." : $"{option.Label} no salió y el rival aprovechó la situación.", Effects = [new() { Label = "Forma", Value = success ? 7 : -5, Direction = success ? "positive" : "negative" }, new() { Label = "Energía", Value = success ? -3 : -9, Direction = "negative" }, new() { Label = "Reputación", Value = success ? 4 : 0, Direction = success ? "positive" : "neutral" }] };
    }

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

    private void AddExpandedEventCatalog()
    {
        if (_events.Templates.Count >= 90) return;
        var extras = new (string Trigger, string Title, string Game, string Rarity, int Age)[]
        {
            ("life","Cena con tu pareja","Memorama de conversaciones","common",16),("life","Ruptura antes del clasico","Decisión de calma","rare",16),("life","Noche con el vestuario","Dado de disciplina","common",18),("life","Invitacion a un bar","Ruta de dados","common",18),("life","Rumor en redes","Cartas de reputación","common",16),("life","Apoyo familiar","Memorama de prioridades","common",16),("life","Fiesta tras la victoria","Dado de recuperación","rare",18),("life","Conflicto con un amigo","Decisión de confianza","rare",16),("life","Nueva vivienda","Tablero de presupuesto","common",18),("life","Aniversario en semana clave","Memorama de agenda","rare",16),
            ("board","La directiva mejora las canchas","Mapa táctico","common",16),("board","Reunion sobre instalaciones","Tablero de proyecto","common",16),("board","El club pide embajador","Cartas de imagen","common",16),("board","Cambio de entrenador","Memorama táctico","rare",16),("board","Capitania disponible","Dado de liderazgo","rare",18),("board","Viaje de pretemporada","Ruta de dados","common",16),("board","Crisis en la directiva","Tablero de confianza","superrare",16),("board","Nuevo centro medico","Buscaminas de recuperación","rare",16),
            ("finance","Patrocinio local","Negociación","common",18),("finance","Inversion inmobiliaria ficticia","Tablero de presupuesto","rare",18),("finance","Asesor financiero","Cartas de riesgo","common",18),("finance","Casino ficticio","Dados de suerte","rare",18),("finance","Multa por retraso","Decisión de responsabilidad","common",16),("finance","Marca internacional","Negociación","superrare",18),
            ("integrity","Propuesta de dejarse ganar","Decisión de integridad","superrare",18),("integrity","Apuesta sospechosa","Cartas de integridad","rare",18),("integrity","Filtracion del vestuario","Memorama de confianza","rare",16),("integrity","Presion de un intermediario","Decisión de integridad","superrare",18),("integrity","Regalo inapropiado","Decisión de integridad","rare",18),("integrity","Investigacion del club","Tablero de reputación","superrare",18)
        };
        foreach (var item in extras.Take(90 - _events.Templates.Count).Select((x, index) => new { x, index }))
            _events.Templates.Add(new EventTemplate { Id = $"extra-{item.index + 1:00}", Trigger = item.x.Trigger, Title = item.x.Title, MiniGame = item.x.Game, Rarity = item.x.Rarity, MinAge = item.x.Age });
    }

    private static readonly string[] SeasonMiniGames = ["penalty", "freekick", "finish", "pass", "interception", "save", "aerial", "rondo", "mines", "casino"];
    private static bool IsMatchMiniGame(string id) => id is "penalty" or "freekick" or "finish" or "pass" or "interception" or "save" or "aerial";
    private static string MiniGameName(string id) => id switch
    {
        "penalty" => "Penal a las esquinas", "freekick" => "Tiro libre", "finish" => "Definición mano a mano", "pass" => "Último pase",
        "interception" => "Intercepción", "save" => "Parada del portero", "aerial" => "Duelo aéreo", "rondo" => "Rondo de memoria",
        "mines" => "Recuperación segura", "casino" => "Dados de casino ficticio", _ => "Desafío"
    };

    private string SelectSeasonMiniGame(CareerState state)
    {
        var roll = Next(state);
        if (state.Player.Age >= 18 && roll >= .90) return "casino";
        if (roll >= .70) return NextInt(state, 2) == 0 ? "rondo" : "mines";
        var matchGames = SeasonMiniGames.Take(7).ToList();
        return matchGames[NextInt(state, matchGames.Count)];
    }

    private MiniGameChallenge CreateChallenge(CareerState state, string gameId)
    {
        var sequenceLength = gameId is "pass" or "aerial" or "rondo" or "mines" ? 3 + NextInt(state, 2) : 1;
        var boardSize = gameId is "pass" or "interception" or "save" or "aerial" or "rondo" or "mines" ? 9 : 100;
        var targets = Enumerable.Range(0, sequenceLength).Select(_ => NextInt(state, boardSize)).ToList();
        var relevant = gameId switch { "save" => state.Player.Goalkeeping, "interception" => state.Player.Defending, "pass" or "rondo" => state.Player.Passing, "aerial" => state.Player.Physical, _ => state.Player.Shooting };
        var difficulty = Math.Clamp(58 + (Club(state.CurrentClub)?.Prestige ?? 50) / 5 - relevant / 10 + (100 - state.Player.Energy) / 8, 48, 82);
        return new MiniGameChallenge
        {
            GameId = gameId, TargetSequence = targets, BoardSize = boardSize, RequiredScore = difficulty,
            IsLuckGame = gameId == "casino",
            Instructions = gameId switch
            {
                "penalty" => "Detén el marcador dentro de la zona verde.", "freekick" => "Encuentra el golpeo limpio en la zona verde.", "finish" => "Mide el toque final con precisión.",
                "pass" => "Memoriza y pulsa la ruta de pase iluminada.", "interception" => "Lee la trayectoria y pulsa los carriles correctos.", "save" => "Elige la secuencia de paradas correcta.",
                "aerial" => "Completa salto, orientación y contacto.", "rondo" => "Repite la secuencia de pases.", "mines" => "Encuentra las zonas seguras de recuperación.",
                _ => "Elige alto o bajo; el dado decide la fortuna ficticia."
            }
        };
    }

    private bool ResolveMiniGame(CareerState state, MiniGameChallenge challenge, MiniGameSubmission? submission)
    {
        if (submission is null) return false;
        if (challenge.IsLuckGame)
        {
            if (submission.Moves.Count != 1 || submission.Moves[0] is < 0 or > 1) return false;
            var die = 1 + NextInt(state, 6);
            return submission.Moves[0] == (die >= 4 ? 1 : 0);
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
        state.SeasonMiniGameUsed = true;
        if (active.Challenge!.GameId == "casino")
        {
            var amount = Math.Min(Math.Max(100m, state.Player.Money * .02m), 2_500m);
            AddLedger(state, "Casino ficticio", success ? "Tirada ganadora" : "Tirada perdida", success ? amount : -amount);
        }
        else if (success) { state.Player.Form = Cap(state.Player.Form + 5); state.Player.Morale = Cap(state.Player.Morale + 3); }
        else { state.Player.Energy = Floor(state.Player.Energy - 6); state.Player.Form = Floor(state.Player.Form - 3); }
        active.Outcome = success ? $"Superaste {active.MiniGame}." : $"Fallaste {active.MiniGame}; la consecuencia se aplicó.";
        active.Resolution = new EventResolution { Headline = active.Title, Result = success ? "Éxito" : "Fallo", Detail = active.Outcome, Effects = [new EventEffect { Label = "Forma", Value = success ? 5 : -3, Direction = success ? "positive" : "negative" }] };
        state.LastResolution = active.Resolution; state.CompletedEvents.Add(active); state.Timeline.Add(active.Outcome); state.ActiveEvent = null;
        return state;
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
