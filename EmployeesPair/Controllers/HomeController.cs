using EmployeesPair.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace EmployeesPair.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment webHostEnvironment;

        public HomeController(IWebHostEnvironment webHostEnvironment)
        {
            this.webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            if (TempData["ResultModel"] != null)
            {
                List<EntryViewModel>? model = JsonConvert
                    .DeserializeObject<List<EntryViewModel>>(TempData["ResultModel"].ToString());

                return View(model);
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetResult(IFormFile svgFile)
        {
            if (svgFile == null)
            {
                return Redirect(nameof(Index));
            }

            List<Entry> entries = await ExtractEntriesFromCSVAsync(svgFile);

            int firstEmployeeID = int.MinValue;
            int secondEmployeeID = int.MinValue;
            int longestDayStreak = int.MinValue;

            foreach (int currentProjectID in entries.Select(e => e.ProjectID).Distinct())
            {
                List<Entry> filteredEntries = entries
                    .Where(e => e.ProjectID == currentProjectID)
                    .ToList();

                for (int i = 0; i < filteredEntries.Count; i++)
                {
                    for (int j = i + 1; j < filteredEntries.Count; j++)
                    {

                        GetFirstAndLastDate(filteredEntries[i], filteredEntries[j],
                            out DateOnly currentLatestDateFrom, out DateOnly currentLatestDateTo);

                        if (currentLatestDateFrom.DayNumber > currentLatestDateTo.DayNumber)
                        {
                            continue;
                        }

                        int currentDateDiff = currentLatestDateTo.DayNumber - currentLatestDateFrom.DayNumber;

                        if (currentDateDiff > longestDayStreak)
                        {
                            longestDayStreak = currentDateDiff;
                            firstEmployeeID = filteredEntries[i].EmpID;
                            secondEmployeeID = filteredEntries[j].EmpID;
                        }
                    }
                }
            }

            List<EntryViewModel> model = new();

            if (longestDayStreak != int.MinValue)
            {
                model = GetAllCommonEntriesFromPair(entries, firstEmployeeID, secondEmployeeID);
            }

            TempData["ResultModel"] = JsonConvert.SerializeObject(model);

            return Redirect(nameof(Index));
        }

        private static async Task<List<Entry>> ExtractEntriesFromCSVAsync(IFormFile svgFile)
        {
            using MemoryStream memoryStream = new(new byte[svgFile.Length]);
            await svgFile.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            List<Entry> entries = new();

            using (StreamReader reader = new(memoryStream))
            {
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();

                    if (line != null)
                    {
                        string[] inputTokens = line.Split(',')
                            .Select(s => s.Trim())
                            .ToArray();

                        Entry entry = new()
                        {
                            EmpID = int.Parse(inputTokens[0]),
                            ProjectID = int.Parse(inputTokens[1]),
                            DateFrom = DateOnly.Parse(inputTokens[2]),
                            DateTo = inputTokens[3] == "NULL" ? null : DateOnly.Parse(inputTokens[3])
                        };

                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private static void GetFirstAndLastDate(Entry firstEntry, Entry secondEntry,
            out DateOnly startDate, out DateOnly endDate)
        {
            startDate = firstEntry.DateFrom.DayNumber > secondEntry.DateFrom.DayNumber
                            ? firstEntry.DateFrom
                            : secondEntry.DateFrom;

            if (firstEntry.DateTo.HasValue && !secondEntry.DateTo.HasValue)
            {
                endDate = firstEntry.DateTo.Value;
            }
            else if (!firstEntry.DateTo.HasValue && secondEntry.DateTo.HasValue)
            {
                endDate = secondEntry.DateTo.Value;
            }
            else if (!firstEntry.DateTo.HasValue && !secondEntry.DateTo.HasValue)
            {
                endDate = DateOnly.FromDateTime(DateTime.Now);
            }
            else
            {
                endDate = firstEntry.DateTo.Value.DayNumber < secondEntry.DateTo.Value.DayNumber
                    ? firstEntry.DateTo.Value
                    : secondEntry.DateTo.Value;
            }
        }

        private static List<EntryViewModel> GetAllCommonEntriesFromPair(List<Entry> entries,
            int firstEmployeeID, int secondEmployeeID)
        {
            List<EntryViewModel> model = new();

            int[] firstProjectIds = entries
                .Where(e => e.EmpID == firstEmployeeID)
                .Select(e => e.ProjectID)
                .ToArray();

            int[] secondProjectIds = entries
                .Where(e => e.EmpID == secondEmployeeID)
                .Select(e => e.ProjectID)
                .ToArray();

            List<Entry> resultEntries = entries
                .Where(e => e.EmpID == firstEmployeeID || e.EmpID == secondEmployeeID)
                .Where(e => firstProjectIds.Contains(e.ProjectID) && secondProjectIds.Contains(e.ProjectID))
                .OrderBy(e => e.ProjectID)
                .ToList();

            for (int i = 0; i < resultEntries.Count; i += 2)
            {
                GetFirstAndLastDate(resultEntries[i], resultEntries[i + 1], out DateOnly startDate, out DateOnly endDate);

                if (startDate > endDate)
                {
                    continue;
                }

                int currentDateDiff = endDate.DayNumber - startDate.DayNumber;

                model.Add(new EntryViewModel
                {
                    FirstEmployeeID = resultEntries[i].EmpID,
                    SecondEmployeeID = resultEntries[i + 1].EmpID,
                    ProjectID = resultEntries[i].ProjectID,
                    DaysWorked = currentDateDiff
                });
            }

            return model.OrderByDescending(e => e.DaysWorked).ToList();
        }
    }
}