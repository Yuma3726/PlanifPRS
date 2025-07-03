using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;

namespace PlanifPRS.Pages
{
    public class AccessDeniedModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public AccessDeniedModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        public List<AdminUser> AdminUsers { get; set; } = new List<AdminUser>();

        public async Task OnGetAsync()
        {
            try
            {
                // ✅ RÉCUPÉRER LES UTILISATEURS AVEC DROITS ADMIN
                AdminUsers = await GetAdminUsersAsync();

                Console.WriteLine($"✅ [2025-07-02 11:46:31 UTC] Page AccessDenied: {AdminUsers.Count} admin(s) trouvé(s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [2025-07-02 11:46:31 UTC] Erreur récupération admins: {ex.Message}");
                AdminUsers = new List<AdminUser>();
            }
        }

        private async Task<List<AdminUser>> GetAdminUsersAsync()
        {
            try
            {
                var adminUsers = new List<AdminUser>();
                var connection = _context.Database.GetDbConnection();

                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Nom, Prenom, Mail, LoginWindows
                    FROM [PlanifPRS].[dbo].[Utilisateurs] 
                    WHERE Droits = 'Admin' 
                      AND DateDeleted IS NULL
                      AND (Nom IS NOT NULL AND Nom != '')
                    ORDER BY Nom, Prenom";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var nom = reader["Nom"]?.ToString() ?? "";
                    var prenom = reader["Prenom"]?.ToString() ?? "";
                    var mail = reader["Mail"]?.ToString() ?? "";
                    var login = reader["LoginWindows"]?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(nom))
                    {
                        adminUsers.Add(new AdminUser
                        {
                            Nom = nom,
                            Prenom = prenom,
                            Mail = mail,
                            LoginWindows = login,
                            NomComplet = $"{prenom} {nom}".Trim()
                        });
                    }
                }

                return adminUsers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [2025-07-02 11:46:31 UTC] Erreur SQL récupération admins: {ex.Message}");
                return new List<AdminUser>();
            }
        }
    }

    public class AdminUser
    {
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string LoginWindows { get; set; } = string.Empty;
        public string NomComplet { get; set; } = string.Empty;
    }
}