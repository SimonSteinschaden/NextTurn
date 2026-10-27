using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using NextTurn.Api.Models;

namespace NextTurn.Api.Repositories;

public class CsvGameRepository(IConfiguration configuration)
{
    public List<BoardGameRankEntry> LoadRankedGames()
    {
        var configured = configuration["Data:BoardGameRanksCsvPath"] ?? "data/boardgames_ranks.csv";
        var path = Path.GetFullPath(configured, AppContext.BaseDirectory);
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(
            reader,
            new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true }
        );
        var rows = csv.GetRecords<BoardGameCsvRow>()
            .Where(x => x.rank is > 0 and <= 10000)
            .OrderBy(x => x.rank)
            .ToList();
        if (
            rows.Count != 10000
            || rows.Select(r => r.id).Distinct().Count() != 10000
            || !rows.Select(r => r.rank).SequenceEqual(Enumerable.Range(1, 10000))
        )
            throw new InvalidDataException(
                "Die CSV muss exakt 10.000 eindeutige Spiele auf den allgemeinen Rängen 1–10.000 enthalten."
            );
        return rows.Select(x => new BoardGameRankEntry
            {
                BggId = x.id,
                BggRank = x.rank,
                Name = x.name,
                YearPublished = x.yearpublished != 0 ? x.yearpublished : null,
                AverageRating = x.average > 0 ? x.average : null,
                BayesAverage = x.bayesaverage > 0 ? x.bayesaverage : null,
                UsersRated = x.usersrated,
            })
            .ToList();
    }

    private class BoardGameCsvRow
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public int yearpublished { get; set; }
        public int rank { get; set; }
        public double average { get; set; }
        public double bayesaverage { get; set; }
        public int usersrated { get; set; }
    }
}
