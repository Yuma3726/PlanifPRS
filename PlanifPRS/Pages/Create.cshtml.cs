using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using PlanifPRS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PlanifPRS.Pages.Prs
{
    public class CreateModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;
        private readonly FileService _fileService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(PlanifPrsDbContext context, FileService fileService, ILogger<CreateModel> logger)
        {
            _context = context;
            _fileService = fileService;
            _logger = logger;
        }

        [BindProperty]
        public Models.Prs Prs { get; set; }

        [BindProperty]
        public List<IFormFile> UploadedFiles { get; set; }

        [BindProperty]
        public string PrsFolderLinks { get; set; }

        public SelectList LigneList { get; set; }

        public IList<PrsFamille> Familles { get; set; }

        [TempData]
        public string Flash { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        // Propriété pour vérifier les droits utilisateur
        public bool IsAdminOrValidateur => HasRequiredRole();

        // Propriété pour obtenir le login de l'utilisateur actuel
        public string CurrentUserLogin => GetCurrentUserLogin();

        public void OnGet()
        {
            ChargerFamilles();
            ChargerLignes();

            var now = DateTime.Now;
            var rounded = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            // Valeurs par défaut
            Prs = new Models.Prs
            {
                // Le statut est défini automatiquement selon les droits de l'utilisateur
                Statut = IsAdminOrValidateur ? "Validé" : "En attente",
                DateDebut = rounded,
                DateFin = rounded.AddHours(1)
            };

            // Écrase les valeurs par défaut si une date est passée dans l'URL
            if (Request.Query.ContainsKey("start"))
            {
                if (DateTime.TryParse(Request.Query["start"], out var parsedStart))
                {
                    Prs.DateDebut = parsedStart;
                    Prs.DateFin = parsedStart.AddHours(1);
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Début de OnPostAsync");
            _logger.LogInformation($"Fichiers reçus: {UploadedFiles?.Count ?? 0}");
            _logger.LogInformation($"PrsFolderLinks reçu: {PrsFolderLinks}");

            ChargerFamilles();
            ChargerLignes();

            if (Prs.DateDebut >= Prs.DateFin)
            {
                ModelState.AddModelError(string.Empty, "La date de début doit être antérieure à la date de fin.");
            }

            if (Prs.LigneId == 0)
            {
                ModelState.AddModelError("Prs.LigneId", "La sélection d'une ligne est obligatoire.");
            }

            // Définir automatiquement le statut en fonction des droits de l'utilisateur
            Prs.Statut = IsAdminOrValidateur ? "Validé" : "En attente";

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Formulaire invalide. Erreurs: " +
                    string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)));
                return Page();
            }

            try
            {
                _logger.LogInformation("Création d'une nouvelle PRS");

                Prs.DateCreation = DateTime.Now;
                Prs.DerniereModification = DateTime.Now;

                // Définir le login de l'utilisateur qui crée la PRS
                Prs.CreatedByLogin = GetCurrentUserLogin();
                _logger.LogInformation($"Créateur: {Prs.CreatedByLogin}");

                // Nettoyer les emojis et caractères spéciaux des champs textuels
                Prs.Titre = CleanEmojis(Prs.Titre);
                Prs.Equipement = CleanEmojis(Prs.Equipement);
                Prs.BesoinOperateur = CleanEmojis(Prs.BesoinOperateur);
                Prs.PresenceClient = CleanEmojis(Prs.PresenceClient);

                // Si l'utilisateur n'est pas admin/validateur et qu'on est en mode semaine
                if (!IsAdminOrValidateur && Request.Form.ContainsKey("weekMode") && Request.Form["weekMode"] == "true")
                {
                    // Récupérer la semaine sélectionnée
                    if (Request.Form.ContainsKey("selectedWeek") &&
                        DateTime.TryParse(Request.Form["selectedWeek"], out var weekStartDate))
                    {
                        // Définir la période du lundi 00:00 au lundi suivant 00:00
                        var mondayStart = GetMondayOfWeek(weekStartDate);
                        var sundayEnd = mondayStart.AddDays(7); // Lundi suivant à 00:00:00

                        Prs.DateDebut = mondayStart;
                        Prs.DateFin = sundayEnd;
                    }
                }

                // Gestion de la couleur PRS
                if (!IsAdminOrValidateur || string.IsNullOrWhiteSpace(Prs.CouleurPRS))
                {
                    Prs.CouleurPRS = null; // Seuls les admin/validateurs peuvent définir une couleur
                }

                // Ajouter la PRS à la base de données
                _context.Prs.Add(Prs);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"PRS créée avec succès. ID: {Prs.Id}, Titre: {Prs.Titre}");

                // Gérer l'upload des fichiers après la création de la PRS
                if (UploadedFiles != null && UploadedFiles.Any())
                {
                    _logger.LogInformation($"Traitement de {UploadedFiles.Count} fichiers uploadés");

                    // Stocker les fichiers
                    var fileResults = await _fileService.SaveMultipleFilesAsync(
                        UploadedFiles,
                        Prs.Id.ToString(),
                        Prs.Titre ?? "PRS"
                    );

                    int successCount = 0;
                    int errorCount = 0;

                    // Créer les enregistrements de fichiers dans la base de données
                    foreach (var (Success, FilePath, ErrorMsg) in fileResults)
                    {
                        if (Success && !string.IsNullOrEmpty(FilePath))
                        {
                            var file = UploadedFiles[fileResults.IndexOf((Success, FilePath, ErrorMsg))];
                            var prsFichier = new PrsFichier
                            {
                                PrsId = Prs.Id,
                                NomOriginal = file.FileName,
                                CheminFichier = FilePath,
                                TypeMime = file.ContentType,
                                Taille = file.Length,
                                DateUpload = DateTime.Now,
                                UploadParLogin = GetCurrentUserLogin()
                            };

                            _logger.LogInformation($"Ajout du fichier à la BDD: {prsFichier.NomOriginal}");
                            _context.PrsFichiers.Add(prsFichier);
                            successCount++;
                        }
                        else
                        {
                            _logger.LogError($"Erreur lors de l'upload: {ErrorMsg}");
                            errorCount++;
                            ModelState.AddModelError(string.Empty, ErrorMsg);
                        }
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Fichiers traités: {successCount} succès, {errorCount} erreurs");

                    if (successCount > 0)
                    {
                        Flash += $" {successCount} fichier(s) téléchargé(s) avec succès.";
                    }

                    if (errorCount > 0)
                    {
                        ErrorMessage = $"{errorCount} fichier(s) n'ont pas pu être téléchargés. Vérifiez les erreurs.";
                    }
                }
                else
                {
                    _logger.LogInformation("Aucun fichier à uploader");
                }

                // Traitement des liens de dossiers
                if (!string.IsNullOrEmpty(PrsFolderLinks))
                {
                    try
                    {
                        _logger.LogInformation($"Traitement des liens de dossiers. JSON reçu: {PrsFolderLinks}");

                        // Options de désérialisation avec System.Text.Json
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        };

                        // Désérialisation du JSON avec System.Text.Json
                        List<FolderLinkDto> folderLinks = null;
                        try
                        {
                            folderLinks = JsonSerializer.Deserialize<List<FolderLinkDto>>(PrsFolderLinks, options);
                            _logger.LogInformation($"Désérialisation réussie, {folderLinks?.Count ?? 0} liens trouvés");

                            // Log détaillé des objets désérialisés
                            if (folderLinks != null)
                            {
                                foreach (var link in folderLinks)
                                {
                                    _logger.LogInformation($"Lien désérialisé - Chemin: '{link.Chemin}', Description: '{link.Description}'");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Erreur lors de la désérialisation: {ex.Message}");

                            // Extraction manuelle avec expressions régulières si la désérialisation échoue
                            _logger.LogWarning("Tentative d'extraction manuelle des chemins");
                            folderLinks = new List<FolderLinkDto>();

                            // Regex pour extraire path/Chemin et description/Description
                            var pathRegex1 = new Regex(@"""path""\s*:\s*""([^""]+)""");
                            var pathRegex2 = new Regex(@"""Chemin""\s*:\s*""([^""]+)""");
                            var descRegex1 = new Regex(@"""description""\s*:\s*""([^""]*)""");
                            var descRegex2 = new Regex(@"""Description""\s*:\s*""([^""]*)""");

                            var pathMatches1 = pathRegex1.Matches(PrsFolderLinks);
                            var pathMatches2 = pathRegex2.Matches(PrsFolderLinks);
                            var descMatches1 = descRegex1.Matches(PrsFolderLinks);
                            var descMatches2 = descRegex2.Matches(PrsFolderLinks);

                            if (pathMatches1.Count > 0)
                            {
                                for (int i = 0; i < pathMatches1.Count; i++)
                                {
                                    string path = pathMatches1[i].Groups[1].Value;
                                    string desc = (i < descMatches1.Count && descMatches1[i].Groups.Count > 1) ?
                                        descMatches1[i].Groups[1].Value : "";

                                    folderLinks.Add(new FolderLinkDto { Chemin = path, Description = desc });
                                }
                            }
                            else if (pathMatches2.Count > 0)
                            {
                                for (int i = 0; i < pathMatches2.Count; i++)
                                {
                                    string path = pathMatches2[i].Groups[1].Value;
                                    string desc = (i < descMatches2.Count && descMatches2[i].Groups.Count > 1) ?
                                        descMatches2[i].Groups[1].Value : "";

                                    folderLinks.Add(new FolderLinkDto { Chemin = path, Description = desc });
                                }
                            }

                            _logger.LogInformation($"Extraction manuelle: {folderLinks.Count} liens trouvés");
                        }

                        if (folderLinks != null && folderLinks.Any())
                        {
                            int addedCount = 0;
                            foreach (var link in folderLinks)
                            {
                                if (!string.IsNullOrEmpty(link.Chemin))
                                {
                                    // Nettoyer le chemin des doubles barres obliques si nécessaire
                                    string chemin = link.Chemin.Replace("\\\\", "\\");

                                    var lienDossier = new LienDossierPrs
                                    {
                                        PrsId = Prs.Id,
                                        Chemin = chemin,
                                        Description = link.Description ?? "",
                                        DateAjout = DateTime.Now,
                                        AjouteParLogin = GetCurrentUserLogin()
                                    };

                                    _logger.LogInformation($"Ajout du lien de dossier: {lienDossier.Chemin}, Description: {lienDossier.Description}");
                                    _context.LiensDossierPrs.Add(lienDossier);
                                    addedCount++;
                                }
                                else
                                {
                                    _logger.LogWarning($"Lien de dossier ignoré car chemin vide. Description: {link.Description}");
                                }
                            }

                            var changesCount = await _context.SaveChangesAsync();
                            _logger.LogInformation($"SaveChanges: {changesCount} enregistrements modifiés pour les liens de dossiers");

                            if (addedCount > 0)
                            {
                                Flash += $" {addedCount} lien(s) de dossier ajouté(s).";
                            }

                            // Vérification après enregistrement
                            var savedLinks = await _context.LiensDossierPrs
                                .Where(l => l.PrsId == Prs.Id)
                                .ToListAsync();
                            _logger.LogInformation($"Liens vérifiés en BDD après sauvegarde: {savedLinks.Count}");
                            foreach (var savedLink in savedLinks)
                            {
                                _logger.LogInformation($"Lien en BDD - Id: {savedLink.Id}, Chemin: '{savedLink.Chemin}'");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Aucun lien de dossier valide trouvé dans les données JSON");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Erreur lors du traitement des liens de dossiers: {ex.Message}");
                        _logger.LogError($"Stack trace: {ex.StackTrace}");
                        ErrorMessage += " Erreur lors du traitement des liens de dossiers.";
                    }
                }

                Flash = "PRS ajoutée avec succès ✅";

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur lors de l'ajout de la PRS: {ex.Message}";
                ModelState.AddModelError(string.Empty, "Erreur lors de l'ajout de la PRS.");
                _logger.LogError($"Exception lors de la création de la PRS: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return Page();
            }
        }

        private string GetCurrentUserLogin()
        {
            var fullLogin = User.Identity?.Name;

            if (string.IsNullOrEmpty(fullLogin))
                return "Utilisateur inconnu";

            // Extraction du login depuis le format domain\username
            var loginParts = fullLogin.Split('\\');
            return loginParts.Length > 1 ? loginParts[1] : fullLogin;
        }

        private DateTime GetMondayOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date; // .Date pour avoir 00:00:00
        }

        private string CleanEmojis(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Nettoyer les emojis
            string cleanedText = Regex.Replace(input, @"[\u00A0-\u9999\uD800-\uDFFF]", "", RegexOptions.Compiled);

            // Si le texte commence par des caractères communs avec des emojis comme "🏭 CMS" → "CMS"
            cleanedText = Regex.Replace(cleanedText, @"^\s*[^\w]*\s*", "");

            // Cas spécifiques connus
            cleanedText = cleanedText.Replace("👨‍🔧 Besoin opérateur", "Besoin opérateur");
            cleanedText = cleanedText.Replace("❌ Aucun", "Aucun");
            cleanedText = cleanedText.Replace("✅ Client présent", "Client présent");
            cleanedText = cleanedText.Replace("❌ Client absent", "Client absent");
            cleanedText = cleanedText.Replace("❓ Non spécifié", "Non spécifié");

            return cleanedText.Trim();
        }

        private bool HasRequiredRole()
        {
            try
            {
                // Nettoyer le login comme dans votre code Users
                var login = GetCurrentUserLogin();

                if (string.IsNullOrEmpty(login))
                {
                    return false;
                }

                // Chercher l'utilisateur dans la base
                var user = _context.Utilisateurs.FirstOrDefault(u => u.LoginWindows == login && !u.DateDeleted.HasValue);

                if (user == null)
                {
                    return false;
                }

                // Vérifier les droits requis (admin ou validateur)
                var droitsAutorises = new[] { "admin", "validateur" };
                var droitUser = user.Droits?.ToLower() ?? "";

                return droitsAutorises.Contains(droitUser);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur vérification droits: {ex.Message}");
                return false;
            }
        }

        private void ChargerFamilles()
        {
            try
            {
                var famillesList = new List<PrsFamille>();
                var connection = _context.Database.GetDbConnection();
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Libelle, CouleurHex 
                    FROM [PlanifPRS].[dbo].[PRS_Famille] 
                    WHERE Libelle IS NOT NULL AND Libelle != ''
                    ORDER BY Libelle";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Accès par index au lieu du nom pour éviter les erreurs de type
                    var idObj = reader[0]; // Première colonne = Id
                    var libelleObj = reader[1]; // Deuxième colonne = Libelle
                    var couleurObj = reader[2]; // Troisième colonne = CouleurHex

                    // Conversion sécurisée de l'ID
                    int id = 0;
                    if (idObj != null && idObj != DBNull.Value)
                    {
                        if (int.TryParse(idObj.ToString(), out int parsedId))
                        {
                            id = parsedId;
                        }
                    }

                    if (id > 0)
                    {
                        famillesList.Add(new PrsFamille
                        {
                            Id = id,
                            Libelle = libelleObj?.ToString() ?? "Sans nom",
                            CouleurHex = couleurObj?.ToString() ?? "#009dff"
                        });
                    }
                }

                Familles = famillesList;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur ChargerFamilles: {ex.Message}");
                Familles = new List<PrsFamille>();
            }
        }

        private void ChargerLignes()
        {
            try
            {
                var lignesList = new List<SelectListItem>();
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Nom 
                    FROM [PlanifPRS].[dbo].[Lignes] 
                    WHERE activation = 1 AND Nom IS NOT NULL AND Nom != ''
                    ORDER BY Nom";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Accès par index au lieu du nom pour éviter les erreurs de type
                    var idObj = reader[0]; // Première colonne = Id
                    var nomObj = reader[1]; // Deuxième colonne = Nom

                    // Conversion sécurisée de l'ID
                    int id = 0;
                    if (idObj != null && idObj != DBNull.Value)
                    {
                        if (int.TryParse(idObj.ToString(), out int parsedId))
                        {
                            id = parsedId;
                        }
                    }

                    if (id > 0)
                    {
                        lignesList.Add(new SelectListItem
                        {
                            Value = id.ToString(),
                            Text = nomObj?.ToString() ?? "Sans nom"
                        });
                    }
                }

                LigneList = new SelectList(lignesList, "Value", "Text");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur ChargerLignes: {ex.Message}");
                LigneList = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
        }

        // Classe DTO pour désérialiser les liens de dossiers
        public class FolderLinkDto
        {
            // Les propriétés doivent correspondre aux noms dans le JSON du frontend
            public string Chemin { get; set; }
            public string Description { get; set; }

            // Propriétés alternatives pour la compatibilité (avec PropertyNameCaseInsensitive)
            public string path { get => Chemin; set => Chemin = value; }
            public string description { get => Description; set => Description = value; }
        }
    }
}