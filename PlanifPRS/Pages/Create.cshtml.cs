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
        private readonly ChecklistService _checklistService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(PlanifPrsDbContext context, FileService fileService, ChecklistService checklistService, ILogger<CreateModel> logger)
        {
            _context = context;
            _fileService = fileService;
            _checklistService = checklistService;
            _logger = logger;

            // Initialisation des propriétés pour éviter les nulls
            ChecklistModeles = new List<ChecklistModele>();
            Familles = new List<PrsFamille>();
            LigneList = new SelectList(new List<SelectListItem>(), "Value", "Text");
        }

        [BindProperty]
        public Models.Prs Prs { get; set; }

        [BindProperty]
        public List<IFormFile> UploadedFiles { get; set; }

        [BindProperty]
        public string PrsFolderLinks { get; set; }

        [BindProperty]
        public string ChecklistData { get; set; }

        public SelectList LigneList { get; set; }

        public IList<PrsFamille> Familles { get; set; }

        // Propriété pour les modèles de checklist - Initialisée pour éviter les null
        public IList<ChecklistModele> ChecklistModeles { get; set; } = new List<ChecklistModele>();

        [TempData]
        public string Flash { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        // Propriété pour vérifier les droits utilisateur
        public bool IsAdminOrValidateur => HasRequiredRole();

        // Propriété pour obtenir le login de l'utilisateur actuel
        public string CurrentUserLogin => GetCurrentUserLogin();

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Début du chargement de la page Create");

                await ChargerDonneesAsync();

                var now = DateTime.Now;
                var rounded = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

                // Valeurs par défaut
                Prs = new Models.Prs
                {
                    Statut = IsAdminOrValidateur ? "Validé" : "En attente",
                    DateDebut = rounded,
                    DateFin = rounded.AddHours(1)
                };

                if (Request.Query.ContainsKey("start"))
                {
                    if (DateTime.TryParse(Request.Query["start"], out var parsedStart))
                    {
                        Prs.DateDebut = parsedStart;
                        Prs.DateFin = parsedStart.AddHours(1);
                    }
                }

                _logger.LogInformation($"Page Create chargée avec {ChecklistModeles.Count} modèles de checklist");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors du chargement de la page Create: {ex.Message}");
                ErrorMessage = "Erreur lors du chargement de la page. Veuillez réessayer.";
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Début de OnPostAsync");
            _logger.LogInformation($"Fichiers reçus: {UploadedFiles?.Count ?? 0}");
            _logger.LogInformation($"PrsFolderLinks reçu: {PrsFolderLinks}");
            _logger.LogInformation($"ChecklistData reçu: {ChecklistData}");

            await ChargerDonneesAsync();

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
                Prs.CreatedByLogin = GetCurrentUserLogin();

                Prs.Titre = CleanEmojis(Prs.Titre);
                Prs.Equipement = CleanEmojis(Prs.Equipement);
                Prs.BesoinOperateur = CleanEmojis(Prs.BesoinOperateur);
                Prs.PresenceClient = CleanEmojis(Prs.PresenceClient);

                if (!IsAdminOrValidateur && Request.Form.ContainsKey("weekMode") && Request.Form["weekMode"] == "true")
                {
                    if (Request.Form.ContainsKey("selectedWeek") &&
                        DateTime.TryParse(Request.Form["selectedWeek"], out var weekStartDate))
                    {
                        var mondayStart = GetMondayOfWeek(weekStartDate);
                        var sundayEnd = mondayStart.AddDays(7);

                        Prs.DateDebut = mondayStart;
                        Prs.DateFin = sundayEnd;
                    }
                }

                if (!IsAdminOrValidateur || string.IsNullOrWhiteSpace(Prs.CouleurPRS))
                {
                    Prs.CouleurPRS = null;
                }

                _context.Prs.Add(Prs);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"PRS créée avec succès. ID: {Prs.Id}, Titre: {Prs.Titre}");

                // GESTION DE LA CHECKLIST SI PRÉSENTE
                if (!string.IsNullOrWhiteSpace(ChecklistData))
                {
                    try
                    {
                        _logger.LogInformation($"Traitement des données de checklist: {ChecklistData}");

                        var checklistForm = JsonSerializer.Deserialize<ChecklistFormDto>(ChecklistData);
                        if (checklistForm != null)
                        {
                            string userLogin = GetCurrentUserLogin();

                            switch (checklistForm.type)
                            {
                                case "modele":
                                    if (checklistForm.sourceId.HasValue)
                                    {
                                        var success = await _checklistService.ApplyChecklistModeleAsync(Prs.Id, checklistForm.sourceId.Value, userLogin);
                                        if (success)
                                        {
                                            _logger.LogInformation($"Modèle de checklist {checklistForm.sourceId.Value} appliqué avec succès au PRS {Prs.Id}");
                                            Flash += " Checklist créée à partir du modèle.";
                                        }
                                        else
                                        {
                                            _logger.LogWarning($"Échec de l'application du modèle de checklist {checklistForm.sourceId.Value}");
                                            ErrorMessage += " Erreur lors de l'application du modèle de checklist.";
                                        }
                                    }
                                    break;

                                case "copy":
                                    if (checklistForm.sourceId.HasValue)
                                    {
                                        var success = await _checklistService.CopyChecklistFromPrsAsync(Prs.Id, checklistForm.sourceId.Value, userLogin);
                                        if (success)
                                        {
                                            _logger.LogInformation($"Checklist copiée du PRS {checklistForm.sourceId.Value} vers le PRS {Prs.Id}");
                                            Flash += " Checklist copiée à partir d'un autre PRS.";
                                        }
                                        else
                                        {
                                            _logger.LogWarning($"Échec de la copie de la checklist du PRS {checklistForm.sourceId.Value}");
                                            ErrorMessage += " Erreur lors de la copie de la checklist.";
                                        }
                                    }
                                    break;

                                case "custom":
                                    if (checklistForm.elements?.Any() == true)
                                    {
                                        var elements = checklistForm.elements.Select(e => new PrsChecklist
                                        {
                                            Categorie = e.categorie,
                                            SousCategorie = e.sousCategorie,
                                            Libelle = e.libelle,
                                            Ordre = e.ordre,
                                            Obligatoire = e.obligatoire,
                                            EstCoche = false,
                                            Statut = null
                                        }).ToList();

                                        var success = await _checklistService.CreateCustomChecklistAsync(Prs.Id, elements, userLogin);
                                        if (success)
                                        {
                                            _logger.LogInformation($"Checklist personnalisée créée pour le PRS {Prs.Id} avec {elements.Count} éléments");
                                            Flash += " Checklist personnalisée créée.";
                                        }
                                        else
                                        {
                                            _logger.LogWarning($"Échec de la création de la checklist personnalisée pour le PRS {Prs.Id}");
                                            ErrorMessage += " Erreur lors de la création de la checklist personnalisée.";
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Erreur lors du traitement des données de checklist: {ex.Message}");
                        ErrorMessage += " Erreur lors de la création de la checklist.";
                    }
                }

                // -- UPLOAD FICHIERS
                if (UploadedFiles != null && UploadedFiles.Any())
                {
                    _logger.LogInformation($"Traitement de {UploadedFiles.Count} fichiers uploadés");

                    var fileResults = await _fileService.SaveMultipleFilesAsync(
                        UploadedFiles,
                        Prs.Id.ToString(),
                        Prs.Titre ?? "PRS"
                    );

                    int successCount = 0;
                    int errorCount = 0;

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

                // -- TRAITEMENT DES DOSSIERS
                if (!string.IsNullOrEmpty(PrsFolderLinks))
                {
                    try
                    {
                        _logger.LogInformation($"Traitement des liens de dossiers. JSON reçu: {PrsFolderLinks}");

                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        };

                        List<FolderLinkDto> folderLinks = null;
                        try
                        {
                            folderLinks = JsonSerializer.Deserialize<List<FolderLinkDto>>(PrsFolderLinks, options);

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

                            folderLinks = new List<FolderLinkDto>();

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
                        }

                        if (folderLinks != null && folderLinks.Any())
                        {
                            int addedCount = 0;
                            foreach (var link in folderLinks)
                            {
                                if (!string.IsNullOrEmpty(link.Chemin))
                                {
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

        private async Task ChargerDonneesAsync()
        {
            try
            {
                _logger.LogInformation("Début du chargement des données");

                // Charger les données en parallèle pour optimiser les performances
                var tasks = new Task[]
                {
                    Task.Run(() => ChargerFamilles()),
                    Task.Run(() => ChargerLignes()),
                    ChargerChecklistModelesAsync()
                };

                await Task.WhenAll(tasks);

                _logger.LogInformation($"Chargement terminé: {ChecklistModeles.Count} modèles, {Familles.Count} familles, {LigneList.Count()} lignes");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors du chargement des données: {ex.Message}");
            }
        }

        private async Task ChargerChecklistModelesAsync()
        {
            try
            {
                _logger.LogInformation("Début du chargement des modèles de checklist");

                // Vérifier que le service et le contexte sont disponibles
                if (_checklistService == null)
                {
                    _logger.LogError("ChecklistService est null");
                    ChecklistModeles = new List<ChecklistModele>();
                    return;
                }

                if (_context == null)
                {
                    _logger.LogError("Context de base de données est null");
                    ChecklistModeles = new List<ChecklistModele>();
                    return;
                }

                // Test direct sur la base de données pour diagnostiquer le problème
                var totalModeles = await _context.ChecklistModeles.CountAsync();
                var modelesActifs = await _context.ChecklistModeles.CountAsync(m => m.Actif);

                _logger.LogInformation($"Base de données - Total modèles: {totalModeles}, Modèles actifs: {modelesActifs}");

                // Si aucun modèle actif n'existe, créer des modèles par défaut
                if (modelesActifs == 0)
                {
                    _logger.LogInformation("Aucun modèle actif trouvé. Création de modèles par défaut...");
                    await CreerModelesParDefaut();
                }

                // Charger les modèles via le service
                var modeles = await _checklistService.GetChecklistModelesAsync(activeOnly: true);
                ChecklistModeles = modeles ?? new List<ChecklistModele>();

                _logger.LogInformation($"Chargement réussi de {ChecklistModeles.Count} modèles de checklist");

                // Log détaillé des modèles chargés
                foreach (var modele in ChecklistModeles)
                {
                    _logger.LogInformation($"Modèle: ID={modele.Id}, Nom='{modele.Nom}', Famille='{modele.FamilleAffichage}', Actif={modele.Actif}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors du chargement des modèles de checklist: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                ChecklistModeles = new List<ChecklistModele>();
            }
        }

        private async Task CreerModelesParDefaut()
        {
            try
            {
                var userLogin = GetCurrentUserLogin();

                // Modèle générique pour toutes les familles
                var modeleGenerique = new ChecklistModele
                {
                    Nom = "Checklist Standard",
                    Description = "Checklist générique pour toutes les PRS",
                    FamilleEquipement = "Générique",
                    DateCreation = DateTime.Now,
                    CreatedByLogin = userLogin,
                    Actif = true,
                    Elements = new List<ChecklistElementModele>
                    {
                        new ChecklistElementModele
                        {
                            Categorie = "Produit",
                            SousCategorie = "Matière première",
                            Libelle = "Vérifier la disponibilité des matières premières",
                            Priorite = 1,
                            Obligatoire = true
                        },
                        new ChecklistElementModele
                        {
                            Categorie = "Documentation",
                            SousCategorie = "Procédure",
                            Libelle = "Valider la procédure de fabrication",
                            Priorite = 2,
                            Obligatoire = true
                        },
                        new ChecklistElementModele
                        {
                            Categorie = "Process",
                            SousCategorie = "Équipement",
                            Libelle = "Contrôler l'état de l'équipement",
                            Priorite = 2,
                            Obligatoire = true
                        },
                        new ChecklistElementModele
                        {
                            Categorie = "Production",
                            SousCategorie = "Qualité",
                            Libelle = "Effectuer les contrôles qualité",
                            Priorite = 1,
                            Obligatoire = true
                        }
                    }
                };

                // Modèle spécifique pour une famille d'équipement
                var modeleSpecifique = new ChecklistModele
                {
                    Nom = "Checklist Ligne de Production",
                    Description = "Checklist spécialisée pour les lignes de production",
                    FamilleEquipement = "Ligne Production",
                    DateCreation = DateTime.Now,
                    CreatedByLogin = userLogin,
                    Actif = true,
                    Elements = new List<ChecklistElementModele>
                    {
                        new ChecklistElementModele
                        {
                            Categorie = "Produit",
                            SousCategorie = "Composants",
                            Libelle = "Vérifier tous les composants nécessaires",
                            Priorite = 1,
                            Obligatoire = true
                        },
                        new ChecklistElementModele
                        {
                            Categorie = "Process",
                            SousCategorie = "Calibrage",
                            Libelle = "Calibrer les instruments de mesure",
                            Priorite = 2,
                            Obligatoire = true
                        },
                        new ChecklistElementModele
                        {
                            Categorie = "Production",
                            SousCategorie = "Test",
                            Libelle = "Effectuer les tests de fonctionnement",
                            Priorite = 1,
                            Obligatoire = true
                        },
                        new ChecklistElementModele
                        {
                            Categorie = "Documentation",
                            SousCategorie = "Rapport",
                            Libelle = "Rédiger le rapport de validation",
                            Priorite = 3,
                            Obligatoire = false
                        }
                    }
                };

                _context.ChecklistModeles.AddRange(modeleGenerique, modeleSpecifique);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Modèles par défaut créés avec succès");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de la création des modèles par défaut: {ex.Message}");
            }
        }

        private string GetCurrentUserLogin()
        {
            var fullLogin = User.Identity?.Name;

            if (string.IsNullOrEmpty(fullLogin))
                return "Utilisateur inconnu";

            var loginParts = fullLogin.Split('\\');
            return loginParts.Length > 1 ? loginParts[1] : fullLogin;
        }

        private DateTime GetMondayOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private string CleanEmojis(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string cleanedText = Regex.Replace(input, @"[\u00A0-\u9999\uD800-\uDFFF]", "", RegexOptions.Compiled);
            cleanedText = Regex.Replace(cleanedText, @"^\s*[^\w]*\s*", "");
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
                var login = GetCurrentUserLogin();

                if (string.IsNullOrEmpty(login))
                {
                    return false;
                }

                var user = _context.Utilisateurs.FirstOrDefault(u => u.LoginWindows == login && !u.DateDeleted.HasValue);

                if (user == null)
                {
                    return false;
                }

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

                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen)
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
                    var idObj = reader[0];
                    var libelleObj = reader[1];
                    var couleurObj = reader[2];

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

                if (!wasOpen)
                    connection.Close();

                Familles = famillesList;
                _logger.LogInformation($"Chargement de {famillesList.Count} familles réussi");
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

                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen)
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
                    var idObj = reader[0];
                    var nomObj = reader[1];

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

                if (!wasOpen)
                    connection.Close();

                LigneList = new SelectList(lignesList, "Value", "Text");
                _logger.LogInformation($"Chargement de {lignesList.Count} lignes réussi");
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
            public string Chemin { get; set; }
            public string Description { get; set; }
            public string path { get => Chemin; set => Chemin = value; }
            public string description { get => Description; set => Description = value; }
        }

        // DTO checklist pour la désérialisation JS
        public class ChecklistFormDto
        {
            public string type { get; set; }
            public int? sourceId { get; set; }
            public List<ChecklistElementDto> elements { get; set; } = new();
        }

        public class ChecklistElementDto
        {
            public string categorie { get; set; }
            public string sousCategorie { get; set; }
            public string libelle { get; set; }
            public int ordre { get; set; }
            public bool obligatoire { get; set; }
        }
    }
}