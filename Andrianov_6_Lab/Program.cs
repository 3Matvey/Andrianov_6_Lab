var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});
// Register raw SQL executor service
builder.Services.AddSingleton<Andrianov_6_Lab.Services.RawSqlExecutor>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var conn = cfg.GetConnectionString("DefaultConnection");
    var scriptsPath = cfg["SqlScriptsPath"] ?? "..\\..\\";
    return new Andrianov_6_Lab.Services.RawSqlExecutor(conn, scriptsPath);
});
// Register voting service
builder.Services.AddScoped<Andrianov_6_Lab.Services.VotingService>();
// Cookie authentication
builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", opts =>
{
    opts.LoginPath = "/Account/Login";
    opts.AccessDeniedPath = "/Account/Login";
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
// Map simple query page
app.MapGet("/db/queries", () => Results.Redirect("/Db/Queries"));

app.Run();
