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

        $(document).on('click', '.btn-calculate-dates', (e) => {
            this.calculateDatesFromPrsStart();
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
            console.log('Modèle chargé:', modele);

            if (!modele || !modele.elements) {
                throw new Error('Données du modèle invalides');
            }

            this.currentChecklist.elements = modele.elements.map(element => ({
                categorie: element.categorie,
                sousCategorie: element.sousCategorie || '',
                libelle: element.libelle,
                priorite: element.priorite || 3,
                obligatoire: element.obligatoire,
                delaiDefautJours: element.delaiDefautJours
            }));

            this.currentChecklist.sourceId = modeleId;

            this.hideLoading();
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
        const originalContent = `
            <div class="d-flex justify-content-between align-items-center mb-3">
                <h6 class="mb-0">
                    <i class="fas fa-edit me-1"></i>Éléments de la checklist 
                    (<span id="checklistItemCount" class="badge bg-primary">0</span> éléments)
                </h6>
                <div>
                    <button type="button" class="btn btn-info btn-sm me-2 btn-calculate-dates" title="Calculer les dates d'échéance">
                        <i class="fas fa-calendar-alt me-1"></i>Calculer dates
                    </button>
                    <button type="button" class="btn btn-success btn-sm" id="addChecklistItem">
                        <i class="fas fa-plus me-1"></i>Ajouter un élément
                    </button>
                </div>
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

        // Trier les éléments par priorité puis par catégorie
        const sortedElements = [...this.currentChecklist.elements].sort((a, b) => {
            if (a.priorite !== b.priorite) return a.priorite - b.priorite;
            return a.categorie.localeCompare(b.categorie);
        });

        sortedElements.forEach((element, index) => {
            const html = this.createChecklistItemHtml(element, index);
            container.append(html);
        });

        this.updateItemCount();
    }

    createChecklistItemHtml(element, index) {
        const prioriteOptions = [
            { value: 1, label: '🔴 Critique', color: '#dc3545' },
            { value: 2, label: '🟠 Haute', color: '#fd7e14' },
            { value: 3, label: '🟡 Normale', color: '#ffc107' },
            { value: 4, label: '🔵 Basse', color: '#007bff' },
            { value: 5, label: '⚪ Optionnelle', color: '#6c757d' }
        ];

        const prioriteSelected = element.priorite || 3;
        const prioriteColor = prioriteOptions.find(p => p.value === prioriteSelected)?.color || '#ffc107';

        return `
            <div class="checklist-item mb-3 p-3 border rounded position-relative" data-index="${index}" 
                 style="border-left: 4px solid ${prioriteColor};">
                <div class="row align-items-center">
                    <div class="col-md-2">
                        <label class="form-label fw-bold">Catégorie</label>
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
                        <label class="form-label fw-bold">Sous-catégorie</label>
                        <input type="text" class="form-control form-control-sm" name="sousCategorie" 
                               value="${element.sousCategorie || ''}" placeholder="Optionnel">
                    </div>
                    <div class="col-md-3">
                        <label class="form-label fw-bold">Libellé *</label>
                        <input type="text" class="form-control form-control-sm" name="libelle" 
                               value="${element.libelle}" placeholder="Description de l'élément" required>
                    </div>
                    <div class="col-md-2">
                        <label class="form-label fw-bold">Priorité</label>
                        <select class="form-select form-select-sm" name="priorite">
                            ${prioriteOptions.map(p =>
            `<option value="${p.value}" ${prioriteSelected === p.value ? 'selected' : ''}>${p.label}</option>`
        ).join('')}
                        </select>
                    </div>
                    <div class="col-md-1">
                        <label class="form-label fw-bold">Délai (j)</label>
                        <input type="number" class="form-control form-control-sm" name="delaiDefautJours" 
                               value="${element.delaiDefautJours || ''}" min="0" max="365" placeholder="Jours"
                               title="Nombre de jours depuis le début de la PRS">
                    </div>
                    <div class="col-md-1">
                        <label class="form-label fw-bold">Obligatoire</label>
                        <div class="form-check form-switch mt-2">
                            <input class="form-check-input" type="checkbox" name="obligatoire" 
                                   ${element.obligatoire ? 'checked' : ''}>
                            <label class="form-check-label small">
                                ${element.obligatoire ? 'Oui' : 'Non'}
                            </label>
                        </div>
                    </div>
                    <div class="col-md-1">
                        <button type="button" class="btn btn-danger btn-sm btn-remove-item mt-4" 
                                title="Supprimer cet élément">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </div>
                
                <!-- Badge de priorité en haut à droite -->
                <div class="position-absolute top-0 end-0 mt-2 me-2">
                    <span class="badge" style="background-color: ${prioriteColor}; color: white;">
                        ${prioriteOptions.find(p => p.value === prioriteSelected)?.label || '🟡 Normale'}
                    </span>
                </div>
                
                <!-- Indicateur d'échéance si délai défini -->
                ${element.delaiDefautJours ? `
                    <div class="mt-2">
                        <small class="text-muted">
                            <i class="fas fa-clock me-1"></i>
                            Échéance: ${element.delaiDefautJours} jour${element.delaiDefautJours > 1 ? 's' : ''} après le début de la PRS
                        </small>
                    </div>
                ` : ''}
            </div>
        `;
    }
    addNewChecklistItem() {
        const newElement = {
            categorie: '',
            sousCategorie: '',
            libelle: '',
            priorite: 3,
            obligatoire: false,
            delaiDefautJours: null
        };

        this.currentChecklist.elements.push(newElement);
        this.renderChecklistItems();
        this.updateChecklistData();
    }

    removeChecklistItem(e) {
        const index = $(e.target).closest('.checklist-item').data('index');

        if (confirm('Êtes-vous sûr de vouloir supprimer cet élément ?')) {
            this.currentChecklist.elements.splice(index, 1);
            this.renderChecklistItems();
            this.updateChecklistData();
            this.showNotification('Élément supprimé', 'success');
        }
    }

    updateElementData(e) {
        const $item = $(e.target).closest('.checklist-item');
        const index = $item.data('index');
        const field = $(e.target).attr('name');
        let value = $(e.target).val();

        if (field === 'obligatoire') {
            value = $(e.target).is(':checked');
            // Mettre à jour le texte du label
            const $label = $(e.target).siblings('.form-check-label');
            $label.text(value ? 'Oui' : 'Non');
        } else if (field === 'priorite' || field === 'delaiDefautJours') {
            value = parseInt(value) || (field === 'priorite' ? 3 : null);
        }

        if (this.currentChecklist.elements[index]) {
            this.currentChecklist.elements[index][field] = value;

            // Mettre à jour l'affichage si c'est la priorité qui change
            if (field === 'priorite') {
                this.updatePriorityDisplay($item, value);
            }

            this.updateChecklistData();
            this.updateItemCount();
        }
    }

    updatePriorityDisplay($item, priorite) {
        const prioriteOptions = [
            { value: 1, label: '🔴 Critique', color: '#dc3545' },
            { value: 2, label: '🟠 Haute', color: '#fd7e14' },
            { value: 3, label: '🟡 Normale', color: '#ffc107' },
            { value: 4, label: '🔵 Basse', color: '#007bff' },
            { value: 5, label: '⚪ Optionnelle', color: '#6c757d' }
        ];

        const prioriteInfo = prioriteOptions.find(p => p.value === priorite) || prioriteOptions[2];

        // Mettre à jour la couleur de la bordure
        $item.css('border-left-color', prioriteInfo.color);

        // Mettre à jour le badge de priorité
        const $badge = $item.find('.badge');
        $badge.css('background-color', prioriteInfo.color).text(prioriteInfo.label);
    }

    calculateDatesFromPrsStart() {
        // Récupérer la date de début de la PRS depuis le formulaire
        const dateDebut = $('input[name="DateDebut"]').val() ||
            $('input[name="dateDebut"]').val() ||
            new Date().toISOString().split('T')[0];

        if (!dateDebut) {
            this.showNotification('Impossible de trouver la date de début de la PRS', 'warning');
            return;
        }

        const startDate = new Date(dateDebut);
        let calculatedCount = 0;

        // Calculer les dates pour chaque élément qui a un délai défini
        this.currentChecklist.elements.forEach((element, index) => {
            if (element.delaiDefautJours && element.delaiDefautJours > 0) {
                const echeanceDate = new Date(startDate);
                echeanceDate.setDate(startDate.getDate() + element.delaiDefautJours);

                // Afficher l'information calculée (optionnel - pour debug)
                console.log(`Élément ${index}: ${element.libelle} - Échéance: ${echeanceDate.toLocaleDateString()}`);
                calculatedCount++;
            }
        });

        if (calculatedCount > 0) {
            this.showNotification(`Dates calculées pour ${calculatedCount} élément(s)`, 'success');
            this.renderChecklistItems(); // Re-rendre pour afficher les infos mises à jour
        } else {
            this.showNotification('Aucun élément n\'a de délai défini', 'info');
        }
    }

    updateItemCount() {
        const count = this.currentChecklist.elements.length;
        const obligatoireCount = this.currentChecklist.elements.filter(e => e.obligatoire).length;
        const criticalCount = this.currentChecklist.elements.filter(e => e.priorite === 1).length;

        $('#checklistItemCount').text(count);

        // Afficher des statistiques supplémentaires
        const $counter = $('#checklistItemCount').parent();
        $counter.attr('title',
            `Total: ${count} | Obligatoires: ${obligatoireCount} | Critiques: ${criticalCount}`
        );
    }

    updateChecklistData() {
        const checklistData = {
            type: this.currentChecklist.type,
            sourceId: this.currentChecklist.sourceId,
            elements: this.currentChecklist.elements
        };

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
                type === 'warning' ? 'fas fa-exclamation-circle' :
                    'fas fa-info-circle';

        const notification = $(`
            <div class="alert ${alertClass} alert-dismissible fade show position-fixed" 
                 style="top: 20px; right: 20px; z-index: 1050; min-width: 300px; max-width: 500px;">
                <i class="${icon} me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `);

        $('body').append(notification);

        setTimeout(() => {
            notification.alert('close');
        }, 5000);
    }

    getChecklistData() {
        return this.currentChecklist;
    }

    validateChecklist() {
        if (this.currentChecklist.type === '' || this.currentChecklist.type === null) {
            return { isValid: true, message: '' };
        }

        if (this.currentChecklist.elements.length === 0) {
            return { isValid: false, message: 'La checklist ne peut pas être vide.' };
        }

        const invalidElements = this.currentChecklist.elements.filter(el => !el.libelle.trim());
        if (invalidElements.length > 0) {
            return { isValid: false, message: 'Tous les éléments de la checklist doivent avoir un libellé.' };
        }

        // Vérifier les priorités
        const invalidPriorities = this.currentChecklist.elements.filter(el => el.priorite < 1 || el.priorite > 5);
        if (invalidPriorities.length > 0) {
            return { isValid: false, message: 'Les priorités doivent être comprises entre 1 et 5.' };
        }

        return { isValid: true, message: '' };
    }
}

// Initialisation automatique quand le DOM est prêt
$(document).ready(() => {
    window.checklistManager = new ChecklistManager();
});