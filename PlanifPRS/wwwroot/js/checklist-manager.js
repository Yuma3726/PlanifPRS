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

        // Ajouter un élément
        $(this.options.addButton).on('click', () => {
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

            const response = await fetch(`/api/checklist/modeles/${modeleId}`);
            if (!response.ok) {
                throw new Error(`Erreur HTTP: ${response.status}`);
            }

            const modele = await response.json();
            console.log('Modèle chargé:', modele); // Debug

            // Vérifier que les données sont correctes
            if (!modele || !modele.elements) {
                throw new Error('Données du modèle invalides');
            }

            this.currentChecklist.elements = modele.elements.map(element => ({
                categorie: element.categorie,
                sousCategorie: element.sousCategorie || '',
                libelle: element.libelle,
                ordre: element.ordre,
                obligatoire: element.obligatoire
            }));

            this.currentChecklist.sourceId = modeleId;

            // CORRECTION PRINCIPALE : S'assurer que le loading est caché avant d'afficher l'éditeur
            this.hideLoading();

            // Afficher l'éditeur avec les données
            this.showChecklistEditor();
            this.renderChecklistItems();

            this.showNotification('Modèle de checklist appliqué avec succès', 'success');

        } catch (error) {
            console.error('Erreur lors du chargement du modèle:', error);
            this.hideLoading(); // IMPORTANT : Cacher le loading en cas d'erreur aussi
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

            const response = await fetch(`/api/checklist/search-prs?searchTerm=${encodeURIComponent(query)}&limit=10`);
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

            const response = await fetch(`/api/checklist/prs/${prsId}`);
            if (!response.ok) {
                throw new Error('Erreur lors du chargement de la checklist');
            }

            const data = await response.json();

            this.currentChecklist.elements = data.elements || [];
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
        $(this.options.containerSelector).show();
        this.updateItemCount();
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

        // Trier les éléments par ordre
        const sortedElements = [...this.currentChecklist.elements].sort((a, b) => a.ordre - b.ordre);

        sortedElements.forEach((element, index) => {
            const html = this.createChecklistItemHtml(element, index);
            container.append(html);
        });

        this.updateItemCount();
    }

    createChecklistItemHtml(element, index) {
        const categorieColor = this.getCategorieColor(element.categorie);
        const categorieIcon = this.getCategorieIcon(element.categorie);

        return `
            <div class="checklist-item mb-3 p-3 border rounded" data-index="${index}">
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
                    <div class="col-md-4">
                        <label class="form-label">Libellé</label>
                        <input type="text" class="form-control form-control-sm" name="libelle" 
                               value="${element.libelle}" placeholder="Description de l'élément" required>
                    </div>
                    <div class="col-md-1">
                        <label class="form-label">Ordre</label>
                        <input type="number" class="form-control form-control-sm" name="ordre" 
                               value="${element.ordre}" min="1" required>
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
                    <div class="col-md-1">
                        <button type="button" class="btn btn-danger btn-sm btn-remove-item mt-4">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </div>
            </div>
        `;
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
        const newElement = {
            categorie: '',
            sousCategorie: '',
            libelle: '',
            ordre: this.currentChecklist.elements.length + 1,
            obligatoire: false
        };

        this.currentChecklist.elements.push(newElement);
        this.renderChecklistItems();
        this.updateChecklistData();
    }

    removeChecklistItem(e) {
        const index = $(e.target).closest('.checklist-item').data('index');
        this.currentChecklist.elements.splice(index, 1);

        // Réajuster les ordres
        this.currentChecklist.elements.forEach((element, i) => {
            element.ordre = i + 1;
        });

        this.renderChecklistItems();
        this.updateChecklistData();
    }

    updateElementData(e) {
        const $item = $(e.target).closest('.checklist-item');
        const index = $item.data('index');
        const field = $(e.target).attr('name');
        let value = $(e.target).val();

        if (field === 'obligatoire') {
            value = $(e.target).is(':checked');
        } else if (field === 'ordre') {
            value = parseInt(value) || 1;
        }

        if (this.currentChecklist.elements[index]) {
            this.currentChecklist.elements[index][field] = value;
            this.updateChecklistData();
            this.updateItemCount();
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
            elements: this.currentChecklist.elements
        };

        // Créer ou mettre à jour un champ caché pour les données de checklist
        let $hiddenField = $('#checklistData');
        if ($hiddenField.length === 0) {
            $hiddenField = $('<input type="hidden" id="checklistData" name="ChecklistData">');
            $('form').append($hiddenField);
        }
        $hiddenField.val(JSON.stringify(checklistData));
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
        return this.currentChecklist;
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
    window.checklistManager = new ChecklistManager();
});