namespace CaminoALaGloria.Api;

public record CreateCareerRequest(string Name, string Nationality, int ShirtNumber, string League, string Club, string Position, string Archetype, string Personality);
public record DecisionRequest(CareerState Career, string OptionId, int? SkillScore = null);

public sealed class Player
{
    public string Name { get; set; } = "Promesa";
    public string Nationality { get; set; } = "México";
    public int ShirtNumber { get; set; } = 10;
    public string Position { get; set; } = "Mediocampista";
    public string Archetype { get; set; } = "Equilibrado";
    public string Personality { get; set; } = "Profesional";
    public int Age { get; set; } = 16;
    public int Overall { get; set; } = 58;
    public int Potential { get; set; } = 82;
    public int Form { get; set; } = 60;
    public int Energy { get; set; } = 84;
    public int Reputation { get; set; } = 12;
    public int Money { get; set; } = 1800;
    public int Pace { get; set; }
    public int Shooting { get; set; }
    public int Passing { get; set; }
    public int Dribbling { get; set; }
    public int Defending { get; set; }
    public int Physical { get; set; }
    public int Goalkeeping { get; set; }
    public int SkillMoves { get; set; } = 3;
    public int WeakFoot { get; set; } = 3;
    public int Morale { get; set; } = 60;
    public int CoachRelation { get; set; } = 55;
    public int FansRelation { get; set; } = 50;
    public int MediaRelation { get; set; } = 50;
    public int InjuryRisk { get; set; } = 12;
    public int LastSeasonAppearances { get; set; }
    public int LastSeasonGoals { get; set; }
    public int LastSeasonAssists { get; set; }
    public double LastSeasonAverage { get; set; } = 6.5;
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int Appearances { get; set; }
    public double RatingTotal { get; set; }
}

public sealed class CareerState
{
    public int Version { get; set; } = 1;
    public uint RandomState { get; set; }
    public int Season { get; set; } = 2026;
    public int EventIndex { get; set; }
    public int CurrentMatchday { get; set; }
    public int SeasonEventTarget { get; set; } = 4;
    public List<int> ImportantMatchdays { get; set; } = [];
    public int SeasonStartAppearances { get; set; }
    public int SeasonStartGoals { get; set; }
    public int SeasonStartAssists { get; set; }
    public double SeasonStartRatingTotal { get; set; }
    public bool SeasonComplete { get; set; }
    public bool IsRetired { get; set; }
    public string? RetirementSummary { get; set; }
    public string CurrentClub { get; set; } = "Atlético Horizonte";
    public string CurrentLeague { get; set; } = "Liga del Sol";
    public string? PendingClub { get; set; }
    public string? PendingLeague { get; set; }
    public Player Player { get; set; } = new();
    public List<SeasonEvent> CompletedEvents { get; set; } = [];
    public SeasonEvent? ActiveEvent { get; set; }
    public List<TableRow> Table { get; set; } = [];
    public List<MatchFixture> Fixtures { get; set; } = [];
    public List<TransferOffer> TransferOffers { get; set; } = [];
    public List<string> Trophies { get; set; } = [];
    public List<string> Timeline { get; set; } = [];
}

public sealed class SeasonEvent
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string MiniGame { get; set; } = "Decision";
    public List<EventOption> Options { get; set; } = [];
    public string? Outcome { get; set; }
    public MatchContext? Match { get; set; }
    public int RequiredSelections { get; set; }
}

public sealed class MatchFixture
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Matchday { get; set; }
    public string Competition { get; set; } = "";
    public string Home { get; set; } = "";
    public string Away { get; set; } = "";
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public bool IsPlayed { get; set; }
}

public sealed class MatchContext
{
    public string FixtureId { get; set; } = "";
    public int Matchday { get; set; }
    public string Rival { get; set; } = "";
    public bool IsHome { get; set; }
    public int Minute { get; set; }
    public int TeamGoals { get; set; }
    public int RivalGoals { get; set; }
    public string Stakes { get; set; } = "";
    public bool IsDecisive { get; set; }
}

public sealed class EventOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Risk { get; set; } = "";
}

public sealed class TableRow
{
    public string Club { get; set; } = "";
    public int Played { get; set; }
    public int Points { get; set; }
    public int GoalDifference { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}

public sealed class TransferOffer
{
    public string Club { get; set; } = "";
    public string League { get; set; } = "";
    public int Salary { get; set; }
    public string Role { get; set; } = "Rotación";
}

public sealed class WorldCatalog
{
    public List<string> Nationalities { get; set; } = [];
    public List<Region> Regions { get; set; } = [];
    public List<League> Leagues { get; set; } = [];
    public List<Club> Clubs { get; set; } = [];
    public List<Competition> Competitions { get; set; } = [];
}
public sealed class Region { public string Name { get; set; } = ""; public string Style { get; set; } = ""; }
public sealed class League
{
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public int Prestige { get; set; }
    public int Tier { get; set; }
    public int MatchesPerTeam { get; set; }
    public int PromotionCount { get; set; }
    public int RelegationCount { get; set; }
    public string Format { get; set; } = "";
    public List<string> ClubNames { get; set; } = [];
    public string? PromotionLeague { get; set; }
    public string? RelegationLeague { get; set; }
}
public sealed class Club { public string Name { get; set; } = ""; public string League { get; set; } = ""; public string Region { get; set; } = ""; public int Prestige { get; set; } public string Nickname { get; set; } = ""; }
public sealed class Competition { public string Name { get; set; } = ""; public string Scope { get; set; } = ""; public string Type { get; set; } = ""; }
