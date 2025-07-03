using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PlanifPRS.Controllers.Api
{
    [ApiController]
    [Route("api/ai-suggestions")]
    public class AiSuggestionsController : ControllerBase
    {
        private readonly PlanifPrsDbContext _context;

        public AiSuggestionsController(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [HttpPost("suggest-slot")]
        public async Task<IActionResult> SuggestSlot([FromBody] SlotSuggestionRequest request)
        {
            try
            {
                var suggestions = await GenerateSlotSuggestions(request);

                return Ok(new
                {
                    success = true,
                    suggestions = suggestions.Select(s => new {
                        dateDebut = s.DateDebut.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                        dateFin = s.DateFin.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                        score = s.Score,
                        raison = s.Raison
                    }),
                    metadata = new
                    {
                        timestamp = DateTime.UtcNow,
                        robia_version = "1.0",
                        duration_requested = request.DurationHours,
                        equipement = request.Equipement,
                        working_hours_french = "9h00-17h00 (UTC+2)",
                        french_holidays_excluded = true,
                        day_scoring = "Lun/Mar/Mer=25pts, Jeu=10pts, Ven=5pts"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // ===== AJOUT : GESTION DES JOURS FÉRIÉS FRANÇAIS =====
        private static readonly Dictionary<int, List<DateTime>> FrenchHolidays = new Dictionary<int, List<DateTime>>
        {
            [2025] = new List<DateTime>
            {
                new DateTime(2025, 1, 1),   // Jour de l'An
                new DateTime(2025, 4, 21),  // Lundi de Pâques
                new DateTime(2025, 5, 1),   // Fête du Travail
                new DateTime(2025, 5, 8),   // Victoire 1945
                new DateTime(2025, 5, 29),  // Ascension
                new DateTime(2025, 6, 9),   // Lundi de Pentecôte
                new DateTime(2025, 7, 14),  // Fête Nationale
                new DateTime(2025, 8, 15),  // Assomption
                new DateTime(2025, 11, 1),  // Toussaint
                new DateTime(2025, 11, 11), // Armistice 1918
                new DateTime(2025, 12, 25)  // Noël
            },
            [2026] = new List<DateTime>
            {
                new DateTime(2026, 1, 1),   // Jour de l'An
                new DateTime(2026, 4, 6),   // Lundi de Pâques
                new DateTime(2026, 5, 1),   // Fête du Travail
                new DateTime(2026, 5, 8),   // Victoire 1945
                new DateTime(2026, 5, 14),  // Ascension
                new DateTime(2026, 5, 25),  // Lundi de Pentecôte
                new DateTime(2026, 7, 14),  // Fête Nationale
                new DateTime(2026, 8, 15),  // Assomption
                new DateTime(2026, 11, 1),  // Toussaint
                new DateTime(2026, 11, 11), // Armistice 1918
                new DateTime(2026, 12, 25)  // Noël
            }
        };

        /// <summary>
        /// Vérifie si une date est un jour férié français
        /// </summary>
        private bool IsFreenchHoliday(DateTime date)
        {
            var year = date.Year;
            if (FrenchHolidays.ContainsKey(year))
            {
                return FrenchHolidays[year].Any(h => h.Date == date.Date);
            }

            // Si l'année n'est pas dans notre dictionnaire, calculer les fêtes mobiles
            return IsCalculatedHoliday(date);
        }

        /// <summary>
        /// Calcule les jours fériés mobiles pour une année donnée
        /// </summary>
        private bool IsCalculatedHoliday(DateTime date)
        {
            var year = date.Year;
            var easter = CalculateEaster(year);

            var holidays = new List<DateTime>
            {
                new DateTime(year, 1, 1),   // Jour de l'An
                easter.AddDays(1),          // Lundi de Pâques
                new DateTime(year, 5, 1),   // Fête du Travail
                new DateTime(year, 5, 8),   // Victoire 1945
                easter.AddDays(39),         // Ascension
                easter.AddDays(50),         // Lundi de Pentecôte
                new DateTime(year, 7, 14),  // Fête Nationale
                new DateTime(year, 8, 15),  // Assomption
                new DateTime(year, 11, 1),  // Toussaint
                new DateTime(year, 11, 11), // Armistice 1918
                new DateTime(year, 12, 25)  // Noël
            };

            return holidays.Any(h => h.Date == date.Date);
        }

        /// <summary>
        /// Calcule la date de Pâques pour une année donnée (algorithme de Gauss)
        /// </summary>
        private DateTime CalculateEaster(int year)
        {
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int n = (h + l - 7 * m + 114) / 31;
            int p = (h + l - 7 * m + 114) % 31;

            return new DateTime(year, n, p + 1);
        }

        /// <summary>
        /// Vérifie si un jour est ouvrable (ni weekend, ni férié)
        /// </summary>
        private bool IsWorkingDay(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Saturday &&
                   date.DayOfWeek != DayOfWeek.Sunday &&
                   !IsFreenchHoliday(date);
        }

        /// <summary>
        /// Obtient le nom du jour férié français
        /// </summary>
        private string GetHolidayName(DateTime date)
        {
            if (!IsFreenchHoliday(date)) return null;

            var year = date.Year;
            var easter = CalculateEaster(year);

            var holidayNames = new Dictionary<DateTime, string>
            {
                { new DateTime(year, 1, 1), "Jour de l'An" },
                { easter.AddDays(1), "Lundi de Pâques" },
                { new DateTime(year, 5, 1), "Fête du Travail" },
                { new DateTime(year, 5, 8), "Fête de la Victoire" },
                { easter.AddDays(39), "Ascension" },
                { easter.AddDays(50), "Lundi de Pentecôte" },
                { new DateTime(year, 7, 14), "Fête Nationale" },
                { new DateTime(year, 8, 15), "Assomption" },
                { new DateTime(year, 11, 1), "Toussaint" },
                { new DateTime(year, 11, 11), "Armistice" },
                { new DateTime(year, 12, 25), "Noël" }
            };

            return holidayNames.FirstOrDefault(h => h.Key.Date == date.Date).Value ?? "Jour férié";
        }

        // ===== MODIFICATION DES MÉTHODES EXISTANTES =====

        private async Task<List<SlotSuggestion>> GenerateSlotSuggestions(SlotSuggestionRequest request)
        {
            var suggestions = new List<SlotSuggestion>();

            // Période d'analyse : 7h UTC = 9h FR, 15h UTC = 17h FR
            var startAnalysis = new DateTime(2025, 7, 2, 7, 0, 0, DateTimeKind.Utc);
            var endAnalysis = new DateTime(2025, 7, 29, 15, 0, 0, DateTimeKind.Utc);

            var secteurInfo = await GetSecteurInfoSecure(request.LigneId);
            if (secteurInfo == null)
            {
                return await GenerateBasicSlotSuggestions(request, startAnalysis, endAnalysis);
            }

            var existingPrs = await GetExistingPrsSecure(startAnalysis, endAnalysis);

            if (request.DurationHours > 8)
            {
                suggestions = await GenerateMultiDaySlotSuggestions(startAnalysis, endAnalysis, existingPrs, request, secteurInfo);
            }
            else
            {
                suggestions = await GenerateDailySlotSuggestions(startAnalysis, endAnalysis, existingPrs, request, secteurInfo);
            }

            var topSuggestions = suggestions
                .Where(s => s.Score > 20)
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.DateDebut)
                .Take(5)
                .ToList();

            return topSuggestions;
        }

        private async Task<List<SlotSuggestion>> GenerateDailySlotSuggestions(
            DateTime startAnalysis,
            DateTime endAnalysis,
            List<PrsInfo> existingPrs,
            SlotSuggestionRequest request,
            SecteurInfo secteurInfo)
        {
            var suggestions = new List<SlotSuggestion>();

            for (var day = startAnalysis.Date; day <= endAnalysis.Date; day = day.AddDays(1))
            {
                // ✅ MODIFICATION : Vérifier que le jour est ouvrable (ni weekend ni férié)
                if (!IsWorkingDay(day))
                    continue;

                var daySlots = FindBestSlotsForDay(day, existingPrs, request, secteurInfo);
                suggestions.AddRange(daySlots);
            }

            return suggestions;
        }

        private async Task<List<SlotSuggestion>> GenerateMultiDaySlotSuggestions(
            DateTime startAnalysis,
            DateTime endAnalysis,
            List<PrsInfo> existingPrs,
            SlotSuggestionRequest request,
            SecteurInfo secteurInfo)
        {
            var suggestions = new List<SlotSuggestion>();
            var workingDaysNeeded = Math.Ceiling((double)request.DurationHours / 8);

            for (var testDay = startAnalysis.Date; testDay <= endAnalysis.Date.AddDays(-(int)workingDaysNeeded); testDay = testDay.AddDays(1))
            {
                // ✅ MODIFICATION : Vérifier que le jour de début est ouvrable
                if (!IsWorkingDay(testDay))
                    continue;

                var multiDaySlot = AnalyzeMultiDaySlot(testDay, existingPrs, request, secteurInfo);
                if (multiDaySlot != null)
                {
                    suggestions.Add(multiDaySlot);
                }
            }

            return suggestions;
        }

        private SlotSuggestion AnalyzeMultiDaySlot(DateTime startDay, List<PrsInfo> existingPrs, SlotSuggestionRequest request, SecteurInfo secteurInfo)
        {
            var slotStart = startDay.Date.AddHours(7); // 7h UTC = 9h FR

            DateTime slotEnd;
            var remainingHours = request.DurationHours;
            var currentDay = startDay.Date;

            while (remainingHours > 0)
            {
                // ✅ MODIFICATION : Vérifier que le jour est ouvrable
                if (IsWorkingDay(currentDay))
                {
                    if (remainingHours >= 8)
                    {
                        remainingHours -= 8;
                        if (remainingHours == 0)
                        {
                            slotEnd = currentDay.AddHours(15); // 15h UTC = 17h FR
                            break;
                        }
                    }
                    else
                    {
                        slotEnd = currentDay.AddHours(7 + remainingHours);
                        break;
                    }
                }
                currentDay = currentDay.AddDays(1);
            }
            slotEnd = currentDay.AddHours(15);

            // Vérifier conflits + que tous les jours de la période sont ouvrables
            for (var checkDay = startDay.Date; checkDay <= slotEnd.Date; checkDay = checkDay.AddDays(1))
            {
                // ✅ MODIFICATION : Si un jour n'est pas ouvrable, rejeter ce slot
                if (!IsWorkingDay(checkDay))
                {
                    return null;
                }

                var dayConflicts = existingPrs.Any(p =>
                    p.LigneId == request.LigneId &&
                    p.DateDebut.Date == checkDay);

                if (dayConflicts)
                {
                    return null;
                }
            }

            bool hasSectorConflict = CheckSectorConflictForPeriod(slotStart, slotEnd, existingPrs, secteurInfo);
            var score = CalculateMultiDayScore(slotStart, slotEnd, request, secteurInfo, hasSectorConflict, existingPrs);
            var reason = GenerateMultiDayReason(slotStart, slotEnd, request, secteurInfo, hasSectorConflict);

            return new SlotSuggestion
            {
                DateDebut = slotStart,
                DateFin = slotEnd,
                Score = score,
                Raison = reason
            };
        }

        private bool CheckSectorConflictForPeriod(DateTime slotStart, DateTime slotEnd, List<PrsInfo> existingPrs, SecteurInfo secteurInfo)
        {
            if (!secteurInfo.SecteurId.HasValue)
                return false;

            var currentWeekStart = GetStartOfWeek(slotStart);
            var endWeekStart = GetStartOfWeek(slotEnd);

            while (currentWeekStart <= endWeekStart)
            {
                var weekEnd = currentWeekStart.AddDays(6);

                var weekSectorConflict = existingPrs.Any(p =>
                    p.SecteurId == secteurInfo.SecteurId.Value &&
                    p.DateDebut.Date >= currentWeekStart &&
                    p.DateDebut.Date <= weekEnd &&
                    !(p.DateDebut >= slotStart && p.DateDebut < slotEnd));

                if (weekSectorConflict)
                    return true;

                currentWeekStart = currentWeekStart.AddDays(7);
            }

            return false;
        }

        private List<SlotSuggestion> FindBestSlotsForDay(DateTime day, List<PrsInfo> allExistingPrs, SlotSuggestionRequest request, SecteurInfo secteurInfo)
        {
            var suggestions = new List<SlotSuggestion>();
            var duration = TimeSpan.FromHours(request.DurationHours);

            var workStart = day.Date.AddHours(7);  // 7h UTC = 9h FR
            var workEnd = day.Date.AddHours(15);   // 15h UTC = 17h FR

            var dayPrsOnSameLine = allExistingPrs
                .Where(p => p.LigneId == request.LigneId && p.DateDebut.Date == day.Date)
                .ToList();

            bool hasSectorConflictThisWeek = false;
            List<PrsInfo> weekSectorPrs = new List<PrsInfo>();

            if (secteurInfo.SecteurId.HasValue)
            {
                var weekStart = GetStartOfWeek(day);
                var weekEnd = weekStart.AddDays(6);

                weekSectorPrs = allExistingPrs
                    .Where(p => p.SecteurId == secteurInfo.SecteurId.Value &&
                               p.DateDebut.Date >= weekStart &&
                               p.DateDebut.Date <= weekEnd)
                    .ToList();

                hasSectorConflictThisWeek = weekSectorPrs.Any();
            }

            if (request.DurationHours >= 4)
            {
                var longSlots = new[]
                {
                    new { Start = workStart, End = workStart.AddHours(request.DurationHours), Name = "Matinée complète", Bonus = 25 },
                    new { Start = workStart.AddHours(1), End = workStart.AddHours(1 + request.DurationHours), Name = "Matinée décalée", Bonus = 15 },
                    new { Start = workEnd.AddHours(-request.DurationHours), End = workEnd, Name = "Après-midi complet", Bonus = 10 }
                };

                foreach (var slot in longSlots.Where(s => s.End <= workEnd))
                {
                    bool hasDirectConflict = dayPrsOnSameLine.Any(p =>
                        (slot.Start < p.DateFin && slot.End > p.DateDebut));

                    if (!hasDirectConflict)
                    {
                        var score = CalculateSlotScore(slot.Start, slot.End, dayPrsOnSameLine.Count, allExistingPrs, request, secteurInfo, hasSectorConflictThisWeek, day);
                        var reason = GenerateReason(slot.Start, slot.End, dayPrsOnSameLine.Count, weekSectorPrs, request, secteurInfo, hasSectorConflictThisWeek, day);

                        suggestions.Add(new SlotSuggestion
                        {
                            DateDebut = slot.Start,
                            DateFin = slot.End,
                            Score = score + slot.Bonus,
                            Raison = $"{slot.Name}: {reason}"
                        });
                    }
                }
            }
            else
            {
                for (var start = workStart; start.Add(duration) <= workEnd; start = start.AddHours(1))
                {
                    var end = start.Add(duration);

                    bool hasDirectConflict = dayPrsOnSameLine.Any(p =>
                        (start < p.DateFin && end > p.DateDebut));

                    if (!hasDirectConflict)
                    {
                        var score = CalculateSlotScore(start, end, dayPrsOnSameLine.Count, allExistingPrs, request, secteurInfo, hasSectorConflictThisWeek, day);
                        var reason = GenerateReason(start, end, dayPrsOnSameLine.Count, weekSectorPrs, request, secteurInfo, hasSectorConflictThisWeek, day);

                        suggestions.Add(new SlotSuggestion
                        {
                            DateDebut = start,
                            DateFin = end,
                            Score = score,
                            Raison = reason
                        });
                    }
                }
            }

            return suggestions;
        }

        private int CalculateSlotScore(DateTime start, DateTime end, int dayPrsCount, List<PrsInfo> allPrs, SlotSuggestionRequest request, SecteurInfo secteurInfo, bool hasSectorConflictThisWeek, DateTime day)
        {
            int score = 100;

            if (hasSectorConflictThisWeek)
                score -= 70;

            // Bonus horaire (UTC+2 = heure française)
            if (start.Hour >= 7 && start.Hour < 9)        // 9h-11h FR
                score += 35;
            else if (start.Hour >= 9 && start.Hour < 11)  // 11h-13h FR
                score += 25;
            else if (start.Hour >= 11 && start.Hour < 13) // 13h-15h FR
                score += 15;

            if (start.Hour >= 13) // 15h+ FR
                score -= 25;

            // Bonus équipement
            if (!string.IsNullOrEmpty(request.Equipement))
            {
                if (request.Equipement == "CMS" && start.Hour >= 7 && start.Hour < 9)
                    score += 30;
                if (request.Equipement == "Finition" && start.Hour >= 11 && start.Hour < 13)
                    score += 25;
            }

            // Bonus charge journée
            var dayAllPrs = allPrs.Where(p => p.DateDebut.Date == start.Date).Count();
            if (dayAllPrs == 0)
                score += 40;
            else if (dayAllPrs <= 2)
                score += 25;
            else if (dayAllPrs >= 5)
                score -= 20;

            // ✅ NOUVEAU SYSTÈME DE SCORING PAR JOUR
            switch (start.DayOfWeek)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday:
                case DayOfWeek.Wednesday:
                    // Lundi, Mardi, Mercredi = même score élevé
                    score += 25;
                    // Bonus supplémentaire si lundi après férié
                    if (start.DayOfWeek == DayOfWeek.Monday && IsFreenchHoliday(start.AddDays(-1)))
                        score += 10; // Bonus reprise après férié
                    break;

                case DayOfWeek.Thursday:
                    // Jeudi = un peu moins de points
                    score += 10;
                    // Vérifier si vendredi suivant est férié
                    if (IsFreenchHoliday(start.AddDays(1)))
                        score -= 10; // Malus veille de férié
                    break;

                case DayOfWeek.Friday:
                    // Vendredi = beaucoup moins de points
                    score += 5;
                    // Vérifier si pont (lundi suivant férié)
                    if (IsFreenchHoliday(start.AddDays(3)))
                        score -= 15; // Malus pont
                    break;

                    // Samedi et Dimanche sont déjà exclus par IsWorkingDay()
            }

            if (!hasSectorConflictThisWeek && secteurInfo.SecteurId.HasValue)
                score += 45;

            if (dayPrsCount == 0)
                score += 30;

            if (request.DurationHours == 1 && start.Hour >= 7 && start.Hour < 9)
                score += 15;
            else if (request.DurationHours == 8 && start.Hour == 7)
                score += 20;

            return Math.Max(0, score);
        }

        private int CalculateMultiDayScore(DateTime start, DateTime end, SlotSuggestionRequest request, SecteurInfo secteurInfo, bool hasSectorConflict, List<PrsInfo> allPrs)
        {
            int score = 150;

            if (hasSectorConflict)
                score -= 80;

            // ✅ NOUVEAU SYSTÈME DE SCORING PAR JOUR POUR MULTI-DAY
            switch (start.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    score += 35; // Excellent pour commencer la semaine
                    if (IsFreenchHoliday(start.AddDays(-1)))
                        score += 10; // Bonus après férié
                    break;
                case DayOfWeek.Tuesday:
                    score += 30; // Très bon
                    break;
                case DayOfWeek.Wednesday:
                    score += 25; // Bon
                    break;
                case DayOfWeek.Thursday:
                    score += 5; // Moins favorable
                    break;
                case DayOfWeek.Friday:
                    score -= 10; // Défavorable pour commencer
                    break;
            }

            if (!hasSectorConflict && secteurInfo.SecteurId.HasValue)
                score += 50;

            if (request.DurationHours == 16)
                score += 20;
            else if (request.DurationHours == 24)
                score += 15;
            else if (request.DurationHours == 40)
                score += 25;

            var periodPrsCount = allPrs.Count(p => p.DateDebut.Date >= start.Date && p.DateDebut.Date <= end.Date);
            if (periodPrsCount <= 3)
                score += 30;
            else if (periodPrsCount >= 10)
                score -= 25;

            if (end.DayOfWeek == DayOfWeek.Friday && end.Hour > 13)
                score -= 15;

            return Math.Max(0, score);
        }

        private string GenerateReason(DateTime start, DateTime end, int dayPrsCount, List<PrsInfo> weekSectorPrs, SlotSuggestionRequest request, SecteurInfo secteurInfo, bool hasSectorConflictThisWeek, DateTime day)
        {
            var reasons = new List<string>();
            var frenchHour = start.Hour + 2; // UTC+2

            // ✅ MODIFICATION : Vérifier les jours fériés dans les raisons
            var holidayName = GetHolidayName(start.Date);
            if (!string.IsNullOrEmpty(holidayName))
            {
                reasons.Add($"⚠️ Attention: {holidayName}");
            }

            // Vérifier les jours adjacents
            if (IsFreenchHoliday(start.AddDays(-1)))
                reasons.Add("🎉 Reprise après férié - Excellent");
            if (IsFreenchHoliday(start.AddDays(1)))
                reasons.Add("⚠️ Veille de férié - Risqué");

            if (secteurInfo.SecteurId.HasValue)
            {
                if (!hasSectorConflictThisWeek)
                    reasons.Add($"✅ Secteur {secteurInfo.SecteurNom} libre cette semaine");
                else
                {
                    var conflictDay = weekSectorPrs.FirstOrDefault()?.DateDebut.AddHours(2).ToString("dddd dd/MM", new CultureInfo("fr-FR"));
                    reasons.Add($"⚠️ Conflit secteur {secteurInfo.SecteurNom} le {conflictDay}");
                }
            }

            if (frenchHour >= 9 && frenchHour < 11)
                reasons.Add("🌅 Créneau matinal premium (9h-11h)");
            else if (frenchHour >= 11 && frenchHour < 13)
                reasons.Add("🌄 Bon créneau matinal (11h-13h)");
            else if (frenchHour >= 13 && frenchHour < 15)
                reasons.Add("🌤️ Créneau après-midi correct");

            if (dayPrsCount == 0)
                reasons.Add("📅 Journée totalement libre sur cette ligne");
            else if (dayPrsCount <= 2)
                reasons.Add($"📊 Journée peu chargée ({dayPrsCount} PRS)");
            else if (dayPrsCount >= 4)
                reasons.Add($"⚠️ Journée chargée ({dayPrsCount} PRS)");

            if (!string.IsNullOrEmpty(request.Equipement))
            {
                if (request.Equipement == "CMS" && frenchHour >= 9 && frenchHour < 11)
                    reasons.Add("🔧 Parfait pour CMS matinal (9h-11h)");
                else if (request.Equipement == "Finition" && frenchHour >= 13 && frenchHour < 15)
                    reasons.Add("✨ Idéal pour finition après-midi (13h-15h)");
            }

            // ✅ NOUVEAUX MESSAGES POUR LES JOURS
            switch (start.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    reasons.Add("🚀 Lundi - Début de semaine optimal");
                    break;
                case DayOfWeek.Tuesday:
                    reasons.Add("📈 Mardi - Journée excellente pour la production");
                    break;
                case DayOfWeek.Wednesday:
                    reasons.Add("📊 Mercredi - Milieu de semaine stable");
                    break;
                case DayOfWeek.Thursday:
                    reasons.Add("📉 Jeudi - Fin de semaine approche, moins favorable");
                    break;
                case DayOfWeek.Friday:
                    reasons.Add("⚠️ Vendredi - Attention fin de semaine peu productive");
                    break;
            }

            return reasons.Count > 0 ? string.Join(", ", reasons) : "Créneau disponible";
        }

        private string GenerateMultiDayReason(DateTime start, DateTime end, SlotSuggestionRequest request, SecteurInfo secteurInfo, bool hasSectorConflict)
        {
            var reasons = new List<string>();
            var duration = (end - start).TotalHours;
            var workingDays = Math.Ceiling(duration / 8);

            reasons.Add($"📅 Période de {duration}h sur {workingDays} jours ouvrés (9h-17h FR)");

            // ✅ MODIFICATION : Vérifier fériés dans la période
            var holidaysInPeriod = new List<string>();
            for (var checkDay = start.Date; checkDay <= end.Date; checkDay = checkDay.AddDays(1))
            {
                var holidayName = GetHolidayName(checkDay);
                if (!string.IsNullOrEmpty(holidayName))
                {
                    holidaysInPeriod.Add($"{holidayName} ({checkDay:dd/MM})");
                }
            }

            if (holidaysInPeriod.Any())
            {
                reasons.Add($"🎉 Évite les fériés: {string.Join(", ", holidaysInPeriod)}");
            }

            if (!hasSectorConflict && secteurInfo.SecteurId.HasValue)
                reasons.Add($"✅ Secteur {secteurInfo.SecteurNom} entièrement libre");
            else if (hasSectorConflict)
                reasons.Add($"⚠️ Conflit détecté secteur {secteurInfo.SecteurNom}");

            // ✅ NOUVEAUX MESSAGES POUR MULTI-DAY
            switch (start.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    reasons.Add("🚀 Début lundi 9h - Semaine optimale et productive");
                    break;
                case DayOfWeek.Tuesday:
                    reasons.Add("📈 Début mardi 9h - Très favorable pour production");
                    break;
                case DayOfWeek.Wednesday:
                    reasons.Add("📊 Début mercredi 9h - Milieu de semaine acceptable");
                    break;
                case DayOfWeek.Thursday:
                    reasons.Add("📉 Début jeudi 9h - Fin de semaine moins favorable");
                    break;
                case DayOfWeek.Friday:
                    reasons.Add("⚠️ Début vendredi 9h - Peu recommandé");
                    break;
            }

            if (request.DurationHours == 16)
                reasons.Add("⏰ Période de 2 jours consécutifs");
            else if (request.DurationHours == 24)
                reasons.Add("📊 Période de 3 jours étalés");
            else if (request.DurationHours == 40)
                reasons.Add("📅 Semaine complète de production");

            return string.Join(", ", reasons);
        }

        private async Task<SecteurInfo> GetSecteurInfoSecure(int ligneId)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT l.Id as LigneId, l.Nom as LigneNom, 
                           s.Id as SecteurId, s.Nom as SecteurNom
                    FROM [PlanifPRS].[dbo].[Lignes] l
                    LEFT JOIN [PlanifPRS].[dbo].[Secteur] s ON l.idSecteur = s.id
                    WHERE l.Id = @ligneId";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@ligneId";
                parameter.Value = ligneId;
                command.Parameters.Add(parameter);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var secteurIdObj = reader["SecteurId"];
                    var secteurNomObj = reader["SecteurNom"];

                    int? secteurId = null;
                    if (secteurIdObj != null && secteurIdObj != DBNull.Value)
                    {
                        if (int.TryParse(secteurIdObj.ToString(), out int parsedId))
                        {
                            secteurId = parsedId;
                        }
                    }

                    return new SecteurInfo
                    {
                        LigneId = ligneId,
                        LigneNom = reader["LigneNom"]?.ToString() ?? "Ligne inconnue",
                        SecteurId = secteurId,
                        SecteurNom = secteurNomObj?.ToString() ?? "Secteur inconnu"
                    };
                }
            }
            catch (Exception)
            {
                // Erreur ignorée
            }

            return null;
        }

        private async Task<List<PrsInfo>> GetExistingPrsSecure(DateTime startDate, DateTime endDate)
        {
            var prsList = new List<PrsInfo>();

            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT p.Id, p.DateDebut, p.DateFin, p.LigneId,
                           l.Nom as LigneNom, l.idSecteur,
                           s.id as SecteurId, s.nom as SecteurNom
                    FROM [PlanifPRS].[dbo].[PRS] p
                    LEFT JOIN [PlanifPRS].[dbo].[Lignes] l ON p.LigneId = l.Id
                    LEFT JOIN [PlanifPRS].[dbo].[Secteur] s ON l.idSecteur = s.id
                    WHERE p.DateDebut >= @startDate AND p.DateDebut <= @endDate
                    ORDER BY p.DateDebut";

                var startParam = command.CreateParameter();
                startParam.ParameterName = "@startDate";
                startParam.Value = startDate;
                command.Parameters.Add(startParam);

                var endParam = command.CreateParameter();
                endParam.ParameterName = "@endDate";
                endParam.Value = endDate;
                command.Parameters.Add(endParam);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var prsInfo = new PrsInfo();

                    if (reader["Id"] != null && reader["Id"] != DBNull.Value && int.TryParse(reader["Id"].ToString(), out int prsId))
                        prsInfo.Id = prsId;

                    if (reader["DateDebut"] != null && reader["DateDebut"] != DBNull.Value && DateTime.TryParse(reader["DateDebut"].ToString(), out DateTime dateDebut))
                        prsInfo.DateDebut = dateDebut;

                    if (reader["DateFin"] != null && reader["DateFin"] != DBNull.Value && DateTime.TryParse(reader["DateFin"].ToString(), out DateTime dateFin))
                        prsInfo.DateFin = dateFin;

                    if (reader["LigneId"] != null && reader["LigneId"] != DBNull.Value && int.TryParse(reader["LigneId"].ToString(), out int ligneId))
                        prsInfo.LigneId = ligneId;

                    if (reader["SecteurId"] != null && reader["SecteurId"] != DBNull.Value && int.TryParse(reader["SecteurId"].ToString(), out int secteurId))
                        prsInfo.SecteurId = secteurId;

                    prsInfo.SecteurNom = reader["SecteurNom"]?.ToString();
                    prsList.Add(prsInfo);
                }
            }
            catch (Exception)
            {
                // Erreur ignorée
            }

            return prsList;
        }

        private async Task<List<SlotSuggestion>> GenerateBasicSlotSuggestions(SlotSuggestionRequest request, DateTime startAnalysis, DateTime endAnalysis)
        {
            var suggestions = new List<SlotSuggestion>();

            try
            {
                var sameLigneQuery = await _context.Prs
                    .Where(p => p.LigneId == request.LigneId &&
                               p.DateDebut >= startAnalysis &&
                               p.DateDebut <= endAnalysis)
                    .Select(p => new { p.DateDebut, p.DateFin })
                    .ToListAsync();

                for (var day = startAnalysis; day <= endAnalysis; day = day.AddDays(1))
                {
                    // ✅ MODIFICATION : Vérifier que le jour est ouvrable
                    if (!IsWorkingDay(day))
                        continue;

                    var workStart = day.Date.AddHours(7);  // 7h UTC = 9h FR
                    var workEnd = day.Date.AddHours(15);   // 15h UTC = 17h FR
                    var duration = TimeSpan.FromHours(request.DurationHours);
                    var dayPrs = sameLigneQuery.Where(p => p.DateDebut.Date == day.Date).ToList();

                    for (var start = workStart; start.Add(duration) <= workEnd; start = start.AddHours(1))
                    {
                        var end = start.Add(duration);

                        bool hasConflict = dayPrs.Any(p => (start < p.DateFin && end > p.DateDebut));

                        if (!hasConflict)
                        {
                            var score = CalculateBasicScore(start, dayPrs.Count, request);
                            var reason = GenerateBasicReason(start, dayPrs.Count, request);

                            suggestions.Add(new SlotSuggestion
                            {
                                DateDebut = start,
                                DateFin = end,
                                Score = score,
                                Raison = reason
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Erreur ignorée
            }

            return suggestions.OrderByDescending(s => s.Score).Take(3).ToList();
        }

        private int CalculateBasicScore(DateTime start, int dayPrsCount, SlotSuggestionRequest request)
        {
            int score = 80;
            var frenchHour = start.Hour + 2; // UTC+2

            if (frenchHour >= 9 && frenchHour < 13) score += 30; // 9h-13h FR
            if (frenchHour >= 15) score -= 20; // 15h+ FR
            if (request.Equipement == "CMS" && frenchHour >= 9 && frenchHour < 11) score += 25; // 9h-11h FR
            if (request.Equipement == "Finition" && frenchHour >= 13 && frenchHour < 15) score += 20; // 13h-15h FR
            score += Math.Max(0, 3 - dayPrsCount) * 10;

            // ✅ NOUVEAU SYSTÈME DE SCORING PAR JOUR POUR BASIC
            switch (start.DayOfWeek)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday:
                case DayOfWeek.Wednesday:
                    score += 25;
                    break;
                case DayOfWeek.Thursday:
                    score += 10;
                    break;
                case DayOfWeek.Friday:
                    score += 5;
                    break;
            }

            // ✅ MODIFICATION : Bonus/malus fériés
            if (IsFreenchHoliday(start.AddDays(-1))) score += 25; // Après férié
            if (IsFreenchHoliday(start.AddDays(1))) score -= 15;  // Veille férié

            return Math.Max(0, score);
        }

        private string GenerateBasicReason(DateTime start, int dayPrsCount, SlotSuggestionRequest request)
        {
            var reasons = new List<string>();
            var frenchHour = start.Hour + 2; // UTC+2

            // ✅ MODIFICATION : Vérifier fériés
            if (IsFreenchHoliday(start.AddDays(-1))) reasons.Add("🎉 Reprise après férié");
            if (IsFreenchHoliday(start.AddDays(1))) reasons.Add("⚠️ Veille de férié");

            if (frenchHour >= 9 && frenchHour < 13) reasons.Add("🌅 Matinal optimal (9h-13h)");
            if (dayPrsCount == 0) reasons.Add("📅 Journée libre");
            if (request.Equipement == "CMS" && frenchHour >= 9 && frenchHour < 11) reasons.Add("🔧 CMS matinal (9h-11h)");
            if (request.Equipement == "Finition" && frenchHour >= 13 && frenchHour < 15) reasons.Add("✨ Finition après-midi (13h-15h)");

            // ✅ NOUVEAU : Messages par jour pour basic
            switch (start.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    reasons.Add("🚀 Lundi optimal");
                    break;
                case DayOfWeek.Tuesday:
                    reasons.Add("📈 Mardi excellent");
                    break;
                case DayOfWeek.Wednesday:
                    reasons.Add("📊 Mercredi stable");
                    break;
                case DayOfWeek.Thursday:
                    reasons.Add("📉 Jeudi correct");
                    break;
                case DayOfWeek.Friday:
                    reasons.Add("⚠️ Vendredi moins favorable");
                    break;
            }

            return reasons.Count > 0 ? string.Join(", ", reasons) : "Disponible";
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }
    }

    public class SecteurInfo
    {
        public int LigneId { get; set; }
        public string LigneNom { get; set; }
        public int? SecteurId { get; set; }
        public string SecteurNom { get; set; }
    }

    public class PrsInfo
    {
        public int Id { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public int LigneId { get; set; }
        public int? SecteurId { get; set; }
        public string SecteurNom { get; set; }
    }

    public class SlotSuggestionRequest
    {
        public int LigneId { get; set; }
        public string Equipement { get; set; }
        public int DurationHours { get; set; } = 1;
    }

    public class SlotSuggestion
    {
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public int Score { get; set; }
        public string Raison { get; set; }
    }
}