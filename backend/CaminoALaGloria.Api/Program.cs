using CaminoALaGloria.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

app.MapGet("/api/world", (SimulationEngine engine) => engine.World());
app.MapPost("/api/career", (CreateCareerRequest request, SimulationEngine engine) => Results.Ok(engine.Create(request)));
app.MapPost("/api/career/decision", (DecisionRequest request, SimulationEngine engine) =>
{
    try { return Results.Ok(engine.Decide(request.Career, request.OptionId, request.MiniGame)); }
    catch (InvalidOperationException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.MapPost("/api/career/activity", (ActivityRequest request, SimulationEngine engine) =>
{
    try { return Results.Ok(engine.UseActivity(request.Career, request.ActivityId)); }
    catch (InvalidOperationException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.MapPost("/api/career/shop", (ShopPurchaseRequest request, SimulationEngine engine) =>
{
    try { return Results.Ok(engine.BuyProduct(request.Career, request.ProductId)); }
    catch (InvalidOperationException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.MapPost("/api/career/shop/catalog", (CareerState career, SimulationEngine engine) => Results.Ok(engine.ShopCatalog(career)));
app.MapPost("/api/career/casino/quote", (CasinoQuoteRequest request, SimulationEngine engine) => Results.Ok(engine.CasinoQuote(request.Career, request.Game, request.BetPercent)));
app.MapPost("/api/career/casino/open", (CasinoOpenRequest request, SimulationEngine engine) =>
{
    try { return Results.Ok(engine.OpenCasino(request.Career, request.Game, request.BetPercent)); }
    catch (InvalidOperationException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.MapPost("/api/career/casino/action", (CasinoActionRequest request, SimulationEngine engine) =>
{
    try { return Results.Ok(engine.CasinoAction(request.Career, request.Action)); }
    catch (InvalidOperationException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.MapPost("/api/career/advance", (CareerState career, SimulationEngine engine) =>
{
    try { return Results.Ok(engine.AdvanceToNextEvent(career)); }
    catch (InvalidOperationException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.MapPost("/api/career/advance-season", (CareerState career, SimulationEngine engine) =>
{
    try { return Results.Ok(engine.CompleteSeason(career)); }
    catch (InvalidOperationException error) { return Results.BadRequest(new { error = error.Message }); }
});
app.Run("http://localhost:5098");
