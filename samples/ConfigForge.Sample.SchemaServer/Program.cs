var app = builder.Build();

string schemaDir =
    builder.Configuration["SchemaDirectory"]
    ?? Path.Combine(app.Environment.ContentRootPath, "schemas");
app.MapGet(
    "/schemas.json",
    () =>
    {
        if (!Directory.Exists(schemaDir))
        {
            return Results.Json(Array.Empty<string>());
        }

        string[] files =
        [
            .. Directory
                .EnumerateFiles(schemaDir, "*.json")
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => !name.Equals("schemas.json", StringComparison.OrdinalIgnoreCase)),
        ];

        return Results.Json(files);
    }
);
app.MapGet(
    "/{name}.json",
    (string name) =>
    {
        string file = Path.Combine(schemaDir, Path.GetFileName(name) + ".json");
        return File.Exists(file) ? Results.File(file, "application/json") : Results.NotFound();
    }
);
