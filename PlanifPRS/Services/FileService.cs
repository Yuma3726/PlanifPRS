using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PlanifPRS.Services
{
    public class FileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private const string BASE_DIRECTORY = @"I:\TEMP\PlanifPRS";
        private const long MAX_FILE_SIZE = 104857600; // 100 MB

        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;

            // S'assurer que le répertoire de base existe
            try
            {
                if (!Directory.Exists(BASE_DIRECTORY))
                {
                    Directory.CreateDirectory(BASE_DIRECTORY);
                    _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Répertoire créé: {BASE_DIRECTORY}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Erreur lors de la création du répertoire: {ex.Message}");
            }
        }

        public async Task<(bool Success, string FilePath, string ErrorMessage)> SaveFileAsync(IFormFile file, string prsId, string prsTitle)
        {
            if (file == null)
            {
                _logger.LogWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Tentative d'upload avec un fichier null");
                return (false, null, "Aucun fichier sélectionné");
            }

            if (file.Length == 0)
            {
                _logger.LogWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fichier vide: {file.FileName}");
                return (false, null, "Le fichier est vide");
            }

            if (file.Length > MAX_FILE_SIZE)
            {
                _logger.LogWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fichier trop volumineux: {file.FileName}, {file.Length} bytes");
                return (false, null, $"Le fichier dépasse la taille maximale autorisée de 100 Mo");
            }

            try
            {
                // Créer un nom de dossier valide pour la PRS (basé sur ID et titre) avec la date
                string sanitizedTitle = SanitizeFileName(prsTitle ?? "PRS");
                string prsFolderName = $"{prsId}_{sanitizedTitle}_{DateTime.Now:yyyy-MM-dd}";
                string directoryPath = Path.Combine(BASE_DIRECTORY, prsFolderName);

                _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Chemin du répertoire cible: {directoryPath}");

                // Créer le répertoire s'il n'existe pas
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Répertoire PRS créé: {directoryPath}");
                }

                // Sanitize le nom de fichier mais en gardant le nom original (sans timestamp)
                string originalFileName = file.FileName;
                string fileName = SanitizeFileName(originalFileName);
                string filePath = Path.Combine(directoryPath, fileName);

                _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sauvegarde du fichier: {originalFileName} -> {filePath}");
                _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Type MIME: {file.ContentType}, Taille: {file.Length} bytes");

                // Si le fichier existe déjà, il sera remplacé (FileMode.Create)
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // Vérifier que le fichier a bien été créé
                if (!File.Exists(filePath))
                {
                    _logger.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Le fichier n'a pas été créé après l'upload: {filePath}");
                    return (false, null, "Le fichier n'a pas pu être sauvegardé sur le disque");
                }

                _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fichier sauvegardé avec succès: {filePath}");
                return (true, filePath, null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Erreur lors de l'upload du fichier {file.FileName}: {ex.Message}");
                return (false, null, $"Erreur lors de l'enregistrement du fichier: {ex.Message}");
            }
        }

        public async Task<List<(bool Success, string FilePath, string ErrorMessage)>> SaveMultipleFilesAsync(List<IFormFile> files, string prsId, string prsTitle)
        {
            var results = new List<(bool Success, string FilePath, string ErrorMessage)>();

            if (files == null || files.Count == 0)
            {
                _logger.LogWarning($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Tentative d'upload multiple avec une liste de fichiers vide");
                return results;
            }

            _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sauvegarde de {files.Count} fichiers pour la PRS {prsId}");

            foreach (var file in files)
            {
                var result = await SaveFileAsync(file, prsId, prsTitle);
                results.Add(result);
            }

            return results;
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "fichier";

            // Remplacer les caractères non autorisés par des underscores
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleanName = string.Join("_", fileName.Split(invalidChars));

            // Remplacer les espaces par des underscores
            cleanName = cleanName.Replace(' ', '_');

            // Limiter la longueur du nom pour éviter les problèmes de chemin trop long
            if (cleanName.Length > 100)
            {
                string extension = Path.GetExtension(cleanName);
                cleanName = cleanName.Substring(0, 95) + extension;
            }

            // Supprimer les caractères spéciaux restants
            cleanName = Regex.Replace(cleanName, @"[^\w\._-]", "");

            return cleanName;
        }
    }
}