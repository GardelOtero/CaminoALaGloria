namespace CaminoALaGloria.Api;

public record CreateCareerRequest(string Name, string Nationality, int ShirtNumber, string League, string Club, string Position, string Archetype, string Personality);
public record DecisionRequest(CareerState Career, string OptionId, MiniGameSubmission? MiniGame = null);
public record WorldMarketRequest(CareerState Career);

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
    public decimal Money { get; set; } = 1800m;
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
    public int NationalAppearances { get; set; }
    public int NationalGoals { get; set; }
    public double RatingTotal { get; set; }
}

public sealed class Contract
{
    public string Club { get; set; } = "";
    public string League { get; set; } = "";
    public decimal AnnualGrossEur { get; set; }
    public decimal MonthlyNetEur { get; set; }
    public decimal SigningBonusEur { get; set; }
    public decimal AppearanceBonusEur { get; set; }
    public decimal GoalOrAssistBonusEur { get; set; }
    public decimal TitleBonusEur { get; set; }
    public string Role { get; set; } = "Rotación";
    public int Season { get; set; }
}

public sealed class FinancialLedgerEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Season { get; set; }
    public string Month { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal AmountEur { get; set; }
    public decimal BalanceAfter { get; set; }
}

public sealed class EventEffect
{
    public string Label { get; set; } = "";
    public int Value { get; set; }
    public string Direction { get; set; } = "neutral";
}

public sealed class EventResolution
{
    public string Headline { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Score { get; set; }
    public string Detail { get; set; } = "";
    public List<EventEffect> Effects { get; set; } = [];
}

public sealed class MiniGameChallenge
{
    public string GameId { get; set; } = "";
    public string Mode { get; set; } = "memory";
    public string Instructions { get; set; } = "";
    public List<int> TargetSequence { get; set; } = [];
    public List<int> SafeTiles { get; set; } = [];
    public List<string> Choices { get; set; } = [];
    public int TargetLane { get; set; }
    public int DealerScore { get; set; }
    public int PlayerScore { get; set; }
    public int RequiredScore { get; set; }
    public int BoardSize { get; set; } = 3;
    public bool IsLuckGame { get; set; }
}

public sealed class MiniGameSubmission
{
    public List<int> Moves { get; set; } = [];
    public int? Score { get; set; }
    public string? Choice { get; set; }
}

public sealed class SeasonSummary
{
    public int Season { get; set; }
    public string Club { get; set; } = "";
    public string League { get; set; } = "";
    public int Appearances { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int Minutes { get; set; }
    public decimal AnnualSalaryEur { get; set; }
    public decimal MarketValueEur { get; set; }
    public int NationalAppearances { get; set; }
    public double Average { get; set; }
    public List<string> Titles { get; set; } = [];
    public int FinalPosition { get; set; }
    public List<IndividualAward> Awards { get; set; } = [];
    public string Role { get; set; } = "Rotación";
    public List<string> Milestones { get; set; } = [];
}

public sealed class IndividualAward
{
    public int Season { get; set; }
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Reason { get; set; } = "";
    public int ReputationGain { get; set; }
    public decimal PrizeEur { get; set; }
}

public sealed class SeasonArchive
{
    public SeasonSummary Summary { get; set; } = new();
    public List<SeasonEvent> Events { get; set; } = [];
    public List<string> Timeline { get; set; } = [];
    public List<FinancialLedgerEntry> Ledger { get; set; } = [];
}

public sealed class CareerState
{
    public int Version { get; set; } = 4;
    public uint RandomState { get; set; }
    public int Season { get; set; } = 2026;
    public int EventIndex { get; set; }
    public int CurrentMatchday { get; set; }
    public int SeasonEventTarget { get; set; } = 4;
    public string SeasonMiniGameId { get; set; } = "";
    public bool SeasonMiniGameUsed { get; set; }
    public bool NationalEventUsed { get; set; }
    public bool WorldEventUsed { get; set; }
    public List<string> SeenEventIds { get; set; } = [];
    public Dictionary<string, int> FamilyLastSeason { get; set; } = [];
    public int SalaryMonthsPaid { get; set; }
    public List<int> ImportantMatchdays { get; set; } = [];
    public int SeasonStartAppearances { get; set; }
    public int SeasonStartGoals { get; set; }
    public int SeasonStartAssists { get; set; }
    public double SeasonStartRatingTotal { get; set; }
    public int SeasonStartMinutes { get; set; }
    public int SeasonStartNationalAppearances { get; set; }
    public bool SeasonComplete { get; set; }
    public bool IsRetired { get; set; }
    public string? RetirementSummary { get; set; }
    public string CurrentClub { get; set; } = "Atlético Horizonte";
    public string CurrentLeague { get; set; } = "Liga del Sol";
    public string? PendingClub { get; set; }
    public string? PendingLeague { get; set; }
    public Player Player { get; set; } = new();
    public Contract? Contract { get; set; }
    public List<FinancialLedgerEntry> Ledger { get; set; } = [];
    public List<SeasonArchive> SeasonArchives { get; set; } = [];
    public List<SeasonEvent> CompletedEvents { get; set; } = [];
    public SeasonEvent? ActiveEvent { get; set; }
    public EventResolution? LastResolution { get; set; }
    public List<TableRow> Table { get; set; } = [];
    public List<MatchFixture> Fixtures { get; set; } = [];
    public List<TransferOffer> TransferOffers { get; set; } = [];
    public List<string> Trophies { get; set; } = [];
    public List<IndividualAward> IndividualAwards { get; set; } = [];
    public List<string> Timeline { get; set; } = [];
    public uint WorldSeed { get; set; }
    public List<WorldSeasonRecord> WorldHistory { get; set; } = [];
    public List<WorldTransfer> WorldTransfers { get; set; } = [];
    public List<ClubWorldProfile> WorldClubs { get; set; } = [];
    public NationalTeamProfile? NationalTeam { get; set; }
    public List<NationalCampaign> NationalHistory { get; set; } = [];
    public PlayerMarketProfile MarketProfile { get; set; } = new();
    public List<string> RecentMatchSituations { get; set; } = [];
    public string ClubRole { get; set; } = "Cantera";
    public int SeasonMinutes { get; set; }
    public List<string> Objectives { get; set; } = [];
    public List<string> CareerMilestones { get; set; } = [];
}

public sealed class WorldSeasonRecord
{
    public int Season { get; set; }
    public string Competition { get; set; } = "";
    public string Champion { get; set; } = "";
    public string? RunnerUp { get; set; }
    public List<string> Promoted { get; set; } = [];
    public List<string> Relegated { get; set; } = [];
    public List<TableRow> Table { get; set; } = [];
}

public sealed class WorldTransfer
{
    public int Season { get; set; }
    public string PlayerName { get; set; } = "";
    public string Position { get; set; } = "";
    public string FromClub { get; set; } = "";
    public string ToClub { get; set; } = "";
    public decimal FeeEur { get; set; }
    public string Type { get; set; } = "Traspaso";
}

public sealed class ClubWorldProfile
{
    public string Club { get; set; } = "";
    public string League { get; set; } = "";
    public int HistoricalTitles { get; set; }
    public int RecentStrength { get; set; }
    public int FinancialTier { get; set; }
    public int SquadStrength { get; set; }
    public string RecruitmentProfile { get; set; } = "Equilibrado";
    public decimal TransferBudgetEur { get; set; }
}

public sealed class NationalTeamProfile
{
    public string Country { get; set; } = "";
    public string Confederation { get; set; } = "";
    public int Strength { get; set; }
    public int HistoricalTitles { get; set; }
}

public sealed class NationalCampaign
{
    public int Season { get; set; }
    public string Country { get; set; } = "";
    public string Competition { get; set; } = "";
    public int Appearances { get; set; }
    public int Goals { get; set; }
    public string Outcome { get; set; } = "";
}

public sealed class SeasonEvent
{
    public string Id { get; set; } = "";
    public string? TemplateId { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string MiniGame { get; set; } = "Decisión";
    public string Rarity { get; set; } = "common";
    public List<EventOption> Options { get; set; } = [];
    public string? Outcome { get; set; }
    public EventResolution? Resolution { get; set; }
    public MiniGameChallenge? Challenge { get; set; }
    public MatchContext? Match { get; set; }
    public int RequiredSelections { get; set; }
}

public sealed class MatchFixture { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public int Matchday { get; set; } public string Competition { get; set; } = ""; public string Home { get; set; } = ""; public string Away { get; set; } = ""; public int? HomeGoals { get; set; } public int? AwayGoals { get; set; } public bool IsPlayed { get; set; } }
public sealed class MatchContext { public string FixtureId { get; set; } = ""; public int Matchday { get; set; } public string Rival { get; set; } = ""; public bool IsHome { get; set; } public int Minute { get; set; } public int TeamGoals { get; set; } public int RivalGoals { get; set; } public string Stakes { get; set; } = ""; public bool IsDecisive { get; set; } }
public sealed class EventOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Risk { get; set; } = "";
    public string ActionType { get; set; } = "";
    public List<string> AttributeWeights { get; set; } = [];
    public List<EventEffect> Preview { get; set; } = [];
    public int SuccessBonus { get; set; }
    public bool CanConcedeOnFailure { get; set; }
    public string SuccessOutcome { get; set; } = "";
    public string FailureOutcome { get; set; } = "";
}
public sealed class TableRow { public string Club { get; set; } = ""; public int Played { get; set; } public int Points { get; set; } public int GoalDifference { get; set; } public int Wins { get; set; } public int Draws { get; set; } public int Losses { get; set; } public int GoalsFor { get; set; } public int GoalsAgainst { get; set; } }
public sealed class TransferOffer { public string Club { get; set; } = ""; public string League { get; set; } = ""; public decimal Salary { get; set; } public string Role { get; set; } = "Rotación"; public decimal MonthlyNetEur { get; set; } public decimal SigningBonusEur { get; set; } public decimal ClubBudgetEur { get; set; } public int ClubStrength { get; set; } public string Need { get; set; } = "Refuerzo de plantilla"; public string MarketTier { get; set; } = ""; public int RequiredOverall { get; set; } public int Compatibility { get; set; } public string Reason { get; set; } = ""; }
public sealed class WorldCatalog { public List<string> Nationalities { get; set; } = []; public List<Region> Regions { get; set; } = []; public List<League> Leagues { get; set; } = []; public List<Club> Clubs { get; set; } = []; public List<Competition> Competitions { get; set; } = []; public List<NationalTeamProfile> NationalTeams { get; set; } = []; }
public sealed class PlayerMarketProfile
{
    public int Score { get; set; }
    public decimal EstimatedValueEur { get; set; }
    public string InterestLevel { get; set; } = "Sin seguimiento";
    public List<string> ScoutingClubs { get; set; } = [];
    public string Summary { get; set; } = "Aun construyes tu nombre.";
}
public sealed class EventCatalog { public List<EventTemplate> Templates { get; set; } = []; }
public sealed class EventTemplate
{
    public string Id { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string Category { get; set; } = "";
    public string Family { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string MiniGame { get; set; } = "";
    public string Rarity { get; set; } = "common";
    public int MinAge { get; set; }
    public string SafeOption { get; set; } = "Actuar con calma";
    public string RiskOption { get; set; } = "Asumir el riesgo";
    public string EffectProfile { get; set; } = "balanced";
}
public sealed class Region { public string Name { get; set; } = ""; public string Style { get; set; } = ""; }
public sealed class League { public string Name { get; set; } = ""; public string Region { get; set; } = ""; public int Prestige { get; set; } public int Tier { get; set; } public int MatchesPerTeam { get; set; } public int PromotionCount { get; set; } public int RelegationCount { get; set; } public string Format { get; set; } = ""; public string FormatKey { get; set; } = "league"; public decimal MarketScaleEur { get; set; } = 1_000_000m; public List<string> ClubNames { get; set; } = []; public string? PromotionLeague { get; set; } public string? RelegationLeague { get; set; } }
public sealed class Club { public string Name { get; set; } = ""; public string League { get; set; } = ""; public string Region { get; set; } = ""; public int Prestige { get; set; } public string Nickname { get; set; } = ""; public int HistoricalTitles { get; set; } public int RecentStrength { get; set; } public int FinancialTier { get; set; } = 2; public string RecruitmentProfile { get; set; } = "Equilibrado"; }
public sealed class Competition { public string Name { get; set; } = ""; public string Scope { get; set; } = ""; public string Type { get; set; } = ""; }
