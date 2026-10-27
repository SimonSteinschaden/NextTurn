using CsvHelper;
using CsvHelper.Configuration;
using NextTurn.Api.Models;
using System.Globalization;

namespace NextTurn.Api.Repositories
{
    public class CsvGameRepository
    {
        private readonly IConfiguration _configuration;

        public CsvGameRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<BoardGameRankEntry> LoadRankedGames()
        {
            var csvPath =
                _configuration["Data:BoardGameRanksCsvPath"];

            if (string.IsNullOrWhiteSpace(csvPath))
            {
                throw new InvalidOperationException(
                    "Data:BoardGameRanksCsvPath is missing."
                );
            }

            csvPath = Path.GetFullPath(csvPath, AppContext.BaseDirectory);

            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException(
                    $"CSV file not found: {csvPath}"
                );
            }

            using var reader = new StreamReader(csvPath);

            using var csv = new CsvReader(
                reader,
                new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                });

            var rows =
                csv.GetRecords<BoardGameCsvRow>()
                    .ToList();

            return rows
                .Where(x => x.rank > 0)
                .OrderBy(x => x.rank)
                .Select(x => new BoardGameRankEntry
                {
                    BggId = x.id,
                    BggRank = x.rank,
                    Name = x.name
                })
                .ToList();
        }

        private class BoardGameCsvRow
        {
            public int id { get; set; }

            public string name { get; set; } = "";

            public int yearpublished { get; set; }

            public int rank { get; set; }
        }
    }
}