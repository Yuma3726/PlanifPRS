using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace PlanifPRS.Pages
{
    public class WeeklyMailModel : PageModel
    {
        public string CurrentWeek { get; set; }
        public int WeekNumber { get; set; }
        public string NextWeek { get; set; }
        public int NextWeekNumber { get; set; }
        public List<PrsData> SamplePrsData { get; set; }

        public void OnGet()
        {
            // ✅ UTILISER LA DATE RÉELLE DU SERVEUR
            var today = DateTime.Now;

            // ✅ DEBUG - FORCER LA DATE À JUILLET 2025 POUR TESTER
            // Décommente cette ligne si tu veux tester avec la vraie date actuelle
            // today = new DateTime(2025, 7, 2); // 2 juillet 2025

            // ✅ CALCUL CORRECT DE LA SEMAINE ACTUELLE (ISO 8601)
            WeekNumber = ISOWeek.GetWeekOfYear(today);
            CurrentWeek = GetISOWeek(today);

            // ✅ CALCUL DE LA SEMAINE PROCHAINE
            var nextWeekDate = today.AddDays(7);
            NextWeekNumber = ISOWeek.GetWeekOfYear(nextWeekDate);
            NextWeek = GetISOWeek(nextWeekDate);

            // Données d'exemple pour le développement
            SamplePrsData = new List<PrsData>
            {
                new PrsData
                {
                    DateDebut = DateTime.Now.AddDays(1),
                    Description = "PRS RENAULT SLAVE PCBA - Préparation programme de vernissage",
                    Ligne = "VERNI-5",
                    Commentaires = "pas d'ACI + vernissage 2 flans",
                    PresenceClient = "Client absent"
                },
                new PrsData
                {
                    DateDebut = DateTime.Now.AddDays(2),
                    Description = "PRS EMOTORS M3 unitaire",
                    Ligne = "VERNI-6",
                    Commentaires = "WARNING : 10 plateaux, se limiter à 50 pièces",
                    PresenceClient = "Client absent"
                },
                new PrsData
                {
                    DateDebut = DateTime.Now.AddDays(3),
                    Description = "PRS BONTAZ STLA BO74731136",
                    Ligne = "NXT3",
                    Commentaires = "Démarrage à 9h",
                    PresenceClient = "Client présent"
                }
            };
        }

        private string GetISOWeek(DateTime date)
        {
            var year = ISOWeek.GetYear(date);  // ✅ UTILISER GetYear pour l'année ISO
            var week = ISOWeek.GetWeekOfYear(date);

            return $"{year}-W{week:D2}";
        }
    }

    public class PrsData
    {
        public DateTime DateDebut { get; set; }
        public string Description { get; set; }
        public string Ligne { get; set; }
        public string Commentaires { get; set; }
        public string PresenceClient { get; set; }
    }
}