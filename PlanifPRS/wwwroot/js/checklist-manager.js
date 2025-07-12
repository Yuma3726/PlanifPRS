class ChecklistManager {
    constructor(options = {}) {
        this.options = {
            containerSelector: options.containerSelector || '#checklistEditor',
            itemsContainer: options.itemsContainer || '#checklistItems',
            addButton: options.addButton || '#addChecklistItem',
            searchResults: options.searchResults || '#prsSearchResults',
            typeSelector: options.typeSelector || '#checklistTypeSelector',
            modeleSelector: options.modeleSelector || '#checklistModele',
            searchInput: options.searchInput || '#searchPrsInput',
            ...options
        };

        this.currentChecklist = {
            type: '',
            sourceId: null,
            elements: []
        };

        this.searchTimeout = null;
        // Compteur unique pour les IDs des éléments
        this.elementIdCounter = 0;
        this.init();
    }

    init() {
        this.bindEvents();
        this.updateChecklistData();
    }

    bindEvents() {
        // Gestionnaire pour le changement de type
        $(this.options.typeSelector).on('change', (e) => {
            this.handleTypeChange(e.target.value);
        });

        // Gestionnaire pour la sélection de modèle
        $(this.options.modeleSelector).on('change', (e) => {
            const modeleId = e.target.value;
            if (modeleId) {
                this.loadChecklistModele(modeleId);
            }
        });

        // Gestionnaire pour la recherche PRS
        $(this.options.searchInput).on('input', (e) => {
            clearTimeout(this.searchTimeout);
            this.searchTimeout = setTimeout(() => {
                this.searchPrs(e.target.value);
            }, 300);
        });

        // CORRECTION : Utiliser la délégation d'événements pour le bouton d'ajout
        // car il est créé dynamiquement
        $(document).on('click', '#addChecklistItem', () => {
            console.log('Bouton ajouter cliqué'); // Debug
            this.addNewChecklistItem();
        });

        // Déléguer les événements pour les éléments dynamiques
        $(document).on('change', '.checklist-item input, .checklist-item select', (e) => {
            this.updateElementData(e);
        });

        $(document).on('click', '.btn-remove-item', (e) => {
            this.removeChecklistItem(e);
        });
    }

    handleTypeChange(selectedType) {
        console.log('Type sélectionné:', selectedType); // Debug

        // Cacher tous les sélecteurs
        $('#modeleSelector, #prsSelector').hide();
        $(this.options.searchResults).hide();
        $(this.options.containerSelector).hide();

        // Réinitialiser
        this.currentChecklist = {
            type: selectedType,
            sourceId: null,
            elements: []
        };
        this.elementIdCounter = 0;

        switch (selectedType) {
            case 'modele':
                $('#modeleSelector').show();
                break;
            case 'copy':
                $('#prsSelector').show();
                break;
            case 'custom':
                this.showChecklistEditor();
                break;
        }

        this.updateChecklistData();
    }

    async loadChecklistModele(modeleId) {
        try {
            this.showLoading('Chargement du modèle...');

            const response = await fetch(`/api/checklists/modeles/${modeleId}`);
            if (!response.ok) {
                throw new Error(`Erreur HTTP: ${response.status}`);
            }

            const modele = await response.json();
            console.log('Modèle chargé:', modele);

            // Vérifier que les données sont correctes
            if (!modele || !modele.elements) {
                throw new Error('Données du modèle invalides');
            }

            // CORRECTION : Assigner des IDs uniques aux éléments chargés
            this.currentChecklist.elements = modele.elements.map(element => ({
                id: ++this.elementIdCounter, // ID unique
                categorie: element.categorie,
                sousCategorie: element.sousCategorie || '',
                libelle: element.libelle,
                priorite: element.priorite || 3,
                delaiDefautJours: element.delaiDefautJours || 1,
                obligatoire: element.obligatoire
            }));

            this.currentChecklist.sourceId = modeleId;

            // S'assurer que le loading est caché avant d'afficher l'éditeur
            this.hideLoading();

            // Afficher l'éditeur avec les données
            this.showChecklistEditor();
            this.renderChecklistItems();

            this.showNotification('Modèle de checklist appliqué avec succès', 'success');

        } catch (error) {
            console.error('Erreur lors du chargement du modèle:', error);
            this.hideLoading();
            this.showNotification('Erreur lors du chargement du modèle: ' + error.message, 'danger');
        }
    }

    async searchPrs(query) {
        if (query.length < 2) {
            $(this.options.searchResults).hide();
            return;
        }

        try {
            $(this.options.searchResults).show().html(`
                <div class="text-center py-3">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Recherche...</span>
                    </div>
                </div>
            `);

            const response = await fetch(`/api/checklists/search-prs?searchTerm=${encodeURIComponent(query)}&limit=10`);
            if (!response.ok) {
                throw new Error('Erreur lors de la recherche');
            }

            const results = await response.json();

            if (results.length === 0) {
                $(this.options.searchResults).html(`
                    <div class="alert alert-warning">
                        <i class="fas fa-exclamation-circle me-1"></i>
                        Aucune PRS trouvée pour "${query}"
                    </div>
                `);
                return;
            }

            let html = '<div class="list-group">';
            results.forEach(prs => {
                const badgeClass = prs.pourcentageCompletion === 100 ? 'bg-success' : 'bg-warning';
                html += `
                    <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-center"
                            data-prs-id="${prs.id}">
                        <div>
                            <strong>PRS-${prs.id}</strong> - ${prs.titre}
                            <br><small class="text-muted">${prs.equipement} | ${prs.dateCreation}</small>
                        </div>
                        <span class="badge ${badgeClass}">${prs.pourcentageCompletion}%</span>
                    </button>
                `;
            });
            html += '</div>';

            $(this.options.searchResults).html(html);

            // Gestionnaire pour la sélection d'une PRS
            $(this.options.searchResults).find('.list-group-item').on('click', (e) => {
                const prsId = $(e.currentTarget).data('prs-id');
                this.loadChecklistFromPrs(prsId);
            });

        } catch (error) {
            console.error('Erreur:', error);
            $(this.options.searchResults).html(`
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-triangle me-1"></i>
                    Erreur lors de la recherche
                </div>
            `);
        }
    }

    async loadChecklistFromPrs(prsId) {
        try {
            this.showLoading('Chargement de la checklist...');

            const response = await fetch(`/api/checklists/prs/${prsId}`);
            if (!response.ok) {
                throw new Error('Erreur lors du chargement de la checklist');
            }

            const data = await response.json();

            // CORRECTION : Assigner des IDs uniques aux éléments chargés
            this.currentChecklist.elements = (data.checklist || []).map(item => ({
                id: ++this.elementIdCounter, // ID unique
                categorie: item.categorie || '',
                sousCategorie: item.sousCategorie || '',
                libelle: item.libelle || item.tache || '',
                priorite: item.priorite || 3,
                delaiDefautJours: item.delaiDefautJours || 1,
                obligatoire: item.obligatoire || false
            }));

            this.currentChecklist.sourceId = prsId;

            this.hideLoading();
            this.showChecklistEditor();
            this.renderChecklistItems();

            $(this.options.searchResults).hide();
            this.showNotification('Checklist copiée avec succès', 'success');

        } catch (error) {
            console.error('Erreur:', error);
            this.hideLoading();
            this.showNotification('Erreur lors du chargement de la checklist', 'danger');
        }
    }

    showChecklistEditor() {
        console.log('Affichage de l\'éditeur de checklist'); // Debug

        // Restaurer le contenu de l'éditeur si nécessaire
        this.ensureEditorContent();

        $(this.options.containerSelector).show();
        this.updateItemCount();

        // Si aucun élément, en ajouter un par défaut pour les checklists personnalisées
        if (this.currentChecklist.type === 'custom' && this.currentChecklist.elements.length === 0) {
            this.addNewChecklistItem();
        }
    }

    ensureEditorContent() {
        const container = $(this.options.containerSelector);

        // Vérifier si le contenu de l'éditeur existe
        if (container.find('#checklistItems').length === 0) {
            this.hideLoading(); // Ceci va restaurer le contenu
        }
    }

    showLoading(message = 'Chargement...') {
        // Afficher le loading dans le conteneur principal
        $(this.options.containerSelector).html(`
            <div class="text-center py-5">
                <div class="spinner-border text-primary mb-3" role="status">
                    <span class="visually-hidden">Chargement...</span>
                </div>
                <div class="text-muted">${message}</div>
            </div>
        `).show();
    }

    hideLoading() {
        // Cette méthode va restaurer le contenu original de l'éditeur
        const originalContent = `
            <div class="d-flex justify-content-between align-items-center mb-3">
                <h6 class="mb-0"><i class="fas fa-edit me-1"></i>Éléments de la checklist (<span id="checklistItemCount" class="badge-checklist">0</span> éléments)</h6>
                <button type="button" class="btn btn-success btn-sm" id="addChecklistItem">
                    <i class="fas fa-plus me-1"></i>Ajouter un élément
                </button>
            </div>
            <div id="checklistItems" class="checklist-items-container">
                <!-- Les éléments seront ajoutés ici -->
            </div>
        `;
        $(this.options.containerSelector).html(originalContent);

        console.log('Contenu de l\'éditeur restauré, bouton ajouté'); // Debug
    }

    renderChecklistItems() {
        const container = $(this.options.itemsContainer);
        container.empty();

        if (this.currentChecklist.elements.length === 0) {
            container.html(`
                <div class="alert alert-info text-center">
                    <i class="fas fa-info-circle me-2"></i>
                    Aucun élément dans la checklist. Cliquez sur "Ajouter un élément" pour commencer.
                </div>
            `);
            return;
        }

        // CORRECTION : Ne pas trier pour l'affichage, garder l'ordre d'ajout
        // Les nouveaux éléments apparaîtront en haut
        this.currentChecklist.elements.forEach((element) => {
            const html = this.createChecklistItemHtml(element);
            container.append(html);
        });

        this.updateItemCount();
    }

    createChecklistItemHtml(element) {
        const categorieColor = this.getCategorieColor(element.categorie);
        const prioriteInfo = this.getPrioriteInfo(element.priorite);

        return `
            <div class="checklist-item mb-3 p-3 border rounded" data-element-id="${element.id}" 
                 style="border-left: 4px solid ${prioriteInfo.color};">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <div class="d-flex align-items-center">
                        <span class="badge me-2" style="background-color: ${prioriteInfo.color};">
                            ${prioriteInfo.label}
                        </span>
                        <small class="text-muted">Délai: ${element.delaiDefautJours || 1} jour(s)</small>
                    </div>
                    <button type="button" class="btn btn-danger btn-sm btn-remove-item">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
                
                <div class="row align-items-center">
                    <div class="col-md-2">
                        <label class="form-label">Catégorie</label>
                        <select class="form-select form-select-sm" name="categorie">
                            <option value="">-- Sélectionner --</option>
                            <option value="Produit" ${element.categorie === 'Produit' ? 'selected' : ''}>📦 Produit</option>
                            <option value="Documentation" ${element.categorie === 'Documentation' ? 'selected' : ''}>📄 Documentation</option>
                            <option value="Process" ${element.categorie === 'Process' ? 'selected' : ''}>⚙️ Process</option>
                            <option value="Matière" ${element.categorie === 'Matière' ? 'selected' : ''}>🧱 Matière</option>
                            <option value="Production" ${element.categorie === 'Production' ? 'selected' : ''}>🏭 Production</option>
                        </select>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label">Sous-catégorie</label>
                        <input type="text" class="form-control form-control-sm" name="sousCategorie" 
                               value="${element.sousCategorie || ''}" placeholder="Optionnel">
                    </div>
                    <div class="col-md-3">
                        <label class="form-label">Libellé</label>
                        <input type="text" class="form-control form-control-sm" name="libelle" 
                               value="${element.libelle}" placeholder="Description de l'élément" required>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label">Priorité</label>
                        <select class="form-select form-select-sm" name="priorite">
                            <option value="1" ${element.priorite === 1 ? 'selected' : ''}>🔴 Critique</option>
                            <option value="2" ${element.priorite === 2 ? 'selected' : ''}>🟠 Haute</option>
                            <option value="3" ${element.priorite === 3 ? 'selected' : ''}>🟡 Normale</option>
                            <option value="4" ${element.priorite === 4 ? 'selected' : ''}>🔵 Basse</option>
                            <option value="5" ${element.priorite === 5 ? 'selected' : ''}>⚪ Très basse</option>
                        </select>
                    </div>
                    <div class="col-md-1">
                        <label class="form-label">Délai (j)</label>
                        <input type="number" class="form-control form-control-sm" name="delaiDefautJours" 
                               value="${element.delaiDefautJours || 1}" min="1" max="365" required>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label">Obligatoire</label>
                        <div class="form-check form-switch">
                            <input class="form-check-input" type="checkbox" name="obligatoire" 
                                   ${element.obligatoire ? 'checked' : ''}>
                            <label class="form-check-label">
                                ${element.obligatoire ? 'Oui' : 'Non'}
                            </label>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    getPrioriteInfo(priorite) {
        const priorites = {
            1: { label: '🔴 Critique', color: '#dc3545' },
            2: { label: '🟠 Haute', color: '#fd7e14' },
            3: { label: '🟡 Normale', color: '#ffc107' },
            4: { label: '🔵 Basse', color: '#007bff' },
            5: { label: '⚪ Très basse', color: '#6c757d' }
        };
        return priorites[priorite] || priorites[3];
    }

    getCategorieColor(categorie) {
        const colors = {
            'Produit': '#007bff',
            'Documentation': '#28a745',
            'Process': '#fd7e14',
            'Matière': '#6f42c1',
            'Production': '#dc3545'
        };
        return colors[categorie] || '#6c757d';
    }

    getCategorieIcon(categorie) {
        const icons = {
            'Produit': 'fas fa-box',
            'Documentation': 'fas fa-file-alt',
            'Process': 'fas fa-cogs',
            'Matière': 'fas fa-cubes',
            'Production': 'fas fa-industry'
        };
        return icons[categorie] || 'fas fa-circle';
    }

    addNewChecklistItem() {
        console.log('Ajout d\'un nouvel élément'); // Debug

        const newElement = {
            id: ++this.elementIdCounter, // CORRECTION : ID unique
            categorie: '',
            sousCategorie: '',
            libelle: '',
            priorite: 3,
            delaiDefautJours: 1,
            obligatoire: false
        };

        // CORRECTION : Ajouter en début de tableau pour qu'il apparaisse en haut
        this.currentChecklist.elements.unshift(newElement);
        this.renderChecklistItems();
        this.updateChecklistData();

        console.log('Élément ajouté, total:', this.currentChecklist.elements.length); // Debug

        // Faire défiler vers le haut et mettre le focus sur le premier champ
        $(this.options.itemsContainer).scrollTop(0);
        setTimeout(() => {
            $(this.options.itemsContainer).find('.checklist-item:first-child input[name="libelle"]').focus();
        }, 100);
    }

    removeChecklistItem(e) {
        // CORRECTION : Utiliser l'ID de l'élément au lieu de l'index
        const elementId = parseInt($(e.target).closest('.checklist-item').data('element-id'));
        console.log('Suppression de l\'élément avec ID:', elementId); // Debug

        // Trouver l'index réel dans le tableau
        const elementIndex = this.currentChecklist.elements.findIndex(el => el.id === elementId);

        if (elementIndex !== -1) {
            this.currentChecklist.elements.splice(elementIndex, 1);
            this.renderChecklistItems();
            this.updateChecklistData();
            console.log('Élément supprimé, total restant:', this.currentChecklist.elements.length);
        } else {
            console.error('Élément non trouvé pour suppression, ID:', elementId);
        }
    }

    updateElementData(e) {
        // CORRECTION : Utiliser l'ID de l'élément au lieu de l'index
        const $item = $(e.target).closest('.checklist-item');
        const elementId = parseInt($item.data('element-id'));
        const field = $(e.target).attr('name');
        let value = $(e.target).val();

        if (field === 'obligatoire') {
            value = $(e.target).is(':checked');
        } else if (field === 'priorite' || field === 'delaiDefautJours') {
            value = parseInt(value) || (field === 'priorite' ? 3 : 1);
        }

        // Trouver l'élément par son ID
        const element = this.currentChecklist.elements.find(el => el.id === elementId);
        if (element) {
            element[field] = value;
            this.updateChecklistData();
            this.updateItemCount();

            // Mettre à jour l'affichage de la priorité sans re-rendre tout
            if (field === 'priorite') {
                const prioriteInfo = this.getPrioriteInfo(value);
                $item.find('.badge').first()
                    .css('background-color', prioriteInfo.color)
                    .text(prioriteInfo.label);
                $item.css('border-left-color', prioriteInfo.color);
            }

            // Mettre à jour l'affichage du délai
            if (field === 'delaiDefautJours') {
                $item.find('.text-muted').text(`Délai: ${value} jour(s)`);
            }

            // Mettre à jour le label du switch obligatoire
            if (field === 'obligatoire') {
                $item.find('.form-check-label').text(value ? 'Oui' : 'Non');
            }
        }
    }

    updateItemCount() {
        const count = this.currentChecklist.elements.length;
        $('#checklistItemCount').text(count);
    }

    updateChecklistData() {
        // Mettre à jour les données dans le formulaire principal
        const checklistData = {
            type: this.currentChecklist.type,
            sourceId: this.currentChecklist.sourceId,
            // CORRECTION : Nettoyer les IDs avant de sauvegarder (ils ne sont utiles que pour l'interface)
            elements: this.currentChecklist.elements.map(el => {
                const { id, ...elementWithoutId } = el;
                return elementWithoutId;
            })
        };

        // Créer ou mettre à jour un champ caché pour les données de checklist
        let $hiddenField = $('#checklistData');
        if ($hiddenField.length === 0) {
            $hiddenField = $('<input type="hidden" id="checklistData" name="ChecklistData">');
            $('form').append($hiddenField);
        }
        $hiddenField.val(JSON.stringify(checklistData));

        console.log('Données checklist mises à jour:', checklistData); // Debug
    }

    showNotification(message, type = 'info') {
        const alertClass = `alert-${type}`;
        const icon = type === 'success' ? 'fas fa-check-circle' :
            type === 'danger' ? 'fas fa-exclamation-triangle' :
                'fas fa-info-circle';

        const notification = $(`
            <div class="alert ${alertClass} alert-dismissible fade show position-fixed" 
                 style="top: 20px; right: 20px; z-index: 1050; min-width: 300px;">
                <i class="${icon} me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `);

        $('body').append(notification);

        // Auto-remove after 5 seconds
        setTimeout(() => {
            notification.alert('close');
        }, 5000);
    }

    // Méthode pour obtenir les données de checklist (utilisée lors de la soumission du formulaire)
    getChecklistData() {
        return {
            type: this.currentChecklist.type,
            sourceId: this.currentChecklist.sourceId,
            elements: this.currentChecklist.elements.map(el => {
                const { id, ...elementWithoutId } = el;
                return elementWithoutId;
            })
        };
    }

    // Méthode pour valider la checklist avant soumission
    validateChecklist() {
        if (this.currentChecklist.type === '' || this.currentChecklist.type === null) {
            return { isValid: true, message: '' }; // Pas de checklist sélectionnée, c'est OK
        }

        if (this.currentChecklist.elements.length === 0) {
            return { isValid: false, message: 'La checklist ne peut pas être vide.' };
        }

        // Vérifier que tous les éléments ont un libellé
        const invalidElements = this.currentChecklist.elements.filter(el => !el.libelle.trim());
        if (invalidElements.length > 0) {
            return { isValid: false, message: 'Tous les éléments de la checklist doivent avoir un libellé.' };
        }

        return { isValid: true, message: '' };
    }
}

// Initialisation automatique quand le DOM est prêt
$(document).ready(() => {
    console.log('Initialisation du ChecklistManager'); // Debug
    window.checklistManager = new ChecklistManager();
});