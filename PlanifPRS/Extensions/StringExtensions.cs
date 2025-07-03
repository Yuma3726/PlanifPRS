using System.Net;

namespace PlanifPRS.Extensions
{
    public static class StringExtensions
    {
        // ✅ TES MÉTHODES EXISTANTES (à conserver)
        // ... ton code existant ...

        // ✅ NOUVELLE MÉTHODE POUR DÉCODER LES ENTITÉS HTML
        public static string DecodeHtml(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return WebUtility.HtmlDecode(input);
        }

        // ✅ MÉTHODE ALTERNATIVE AVEC VÉRIFICATION NULL SAFE
        public static string DecodeHtmlSafe(this string? input)
        {
            return string.IsNullOrEmpty(input) ? string.Empty : WebUtility.HtmlDecode(input);
        }
    }
}