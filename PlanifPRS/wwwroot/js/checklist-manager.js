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
            formSelector: options.formSelector || '#create-form', // CORRECTION : Sélecteur spécifique du formulaire
            ...options
        };

        this.currentChecklist = {
            type: '',
            sourceId: null,
            elements: []
        };

        this.searchTimeout = null;
        this.elementIdCounter = 0;
        this.init();
    }

    init() {
        this.bindEvents();
        this.initializeChecklistData();
        this.updateChecklistData();
        this.setupFormValidation();
    }

    // CORRECTION : Initialiser le champ ChecklistData avec le bon formulaire
    initializeChecklistData() {
        console.log('Initialisation du champ ChecklistData...');

        // CORRECTION : Cibler spécifiquement le formulaire principal
        const $form = $(this.options.formSelector);
        if ($form.length === 0) {
            console.error('Formulaire principal non trouvé avec le sélecteur:', this.options.formSelector);
            return;
        }

        let $hiddenField = $('#checklistData');
        if ($hiddenField.length === 0) {
            // CORRECTION : Ajouter le champ directement dans le formulaire spécifique
            $hiddenField = $('<input type="hidden" id="checklistData" name="ChecklistData">');
            $form.append($hiddenField);
            console.log('Champ ChecklistData créé dans le formulaire:', $form[0]);
        }

        const defaultData = {
            type: '',
            sourceId: null,
            elements: []
        };

        $hiddenField.val(JSON.stringify(defaultData));
        console.log('ChecklistData initialisé avec:', defaultData);

        // CORRECTION : Vérifier que le champ est bien dans le formulaire
        console.log('Champ dans le formulaire:', $hiddenField.closest('form')[0] === $form[0]);
    }

    setupFormValidation() {
        // CORRECTION : Cibler spécifiquement le formulaire principal
        $(this.options.formSelector).on('submit', (e) => {
            console.log('Soumission du formulaire détectée');

            if (!this.validateBeforeSubmit()) {
                e.preventDefault();
                return false;
            }

            return true;
        });
    }

    validateBeforeSubmit() {
        console.log('Validation avant soumission...');

        const $form = $(this.options.formSelector);
        let $hiddenField = $('#checklistData');

        // CORRECTION : S'assurer que le champ est dans le bon formulaire
        if ($hiddenField.length === 0 || $hiddenField.closest('form')[0] !== $form[0]) {
            console.log('Champ ChecklistData manquant ou mal placé, recréation...');

            // Supprimer l'ancien champ s'il existe
            $hiddenField.remove();

            // Recréer le champ dans le bon formulaire
            $hiddenField = $('<input type="hidden" id="checklistData" name="ChecklistData">');
            $form.append($hiddenField);

            this.updateChecklistData();
            console.log('Champ ChecklistData recréé dans le formulaire');
        }

        const checklistData = $hiddenField.val();
        console.log('Données checklist avant soumission:', checklistData);
        console.log('Champ dans le formulaire:', $hiddenField.closest('form')[0] === $form[0]);

        try {
            const parsed = JSON.parse(checklistData);
            console.log('JSON valide:', parsed);

            const validation = this.validateChecklist();
            if (!validation.isValid) {
                this.showNotification(validation.message, 'danger');
                return false;
            }

            return true;
        } catch (e) {
            console.error('JSON invalide dans ChecklistData:', e);
            this.updateChecklistData();
            return true;
        }
    }

    bindEvents() {
        $(this.options.typeSelector).on('change', (e) => {
            this.handleTypeChange(e.target.value);
        });

        $(this.options.modeleSelector).on('change', (e) => {
            const modeleId = e.target.value;
            if (modeleId) {
                this.loadChecklistModele(modeleId);
            }
        });

        $(this.options.searchInput).on('input', (e) => {
            clearTimeout(this.searchTimeout);
            this.searchTimeout = setTimeout(() => {
                this.searchPrs(e.target.value);
            }, 300);
        });

        $(document).on('click', '#addChecklistItem', () => {
            console.log('Bouton ajouter cliqué');
            this.addNewChecklistItem();
        });

        $(document).on('change', '.checklist-item input, .checklist-item select', (e) => {
            this.updateElementData(e);
        });

        $(document).on('click', '.btn-remove-item', (e) => {
            this.removeChecklistItem(e);
        });
    }

    handleTypeChange(selectedType) {
        console.log('=== DEBUT handleTypeChange ===');
        console.log('selectedType:', selectedType);
        console.log('currentChecklist avant:', JSON.stringify(this.currentChecklist));

        // Cacher tous les sélecteurs
        $('#modeleSelector, #prsSelector').hide();
        $(this.options.searchResults).hide();
        $(this.options.containerSelector).hide();

        // Réinitialiser SEULEMENT si le type change vraiment
        if (this.currentChecklist.type !== selectedType) {
            this.currentChecklist = {
                type: selectedType,
                sourceId: null,
                elements: []
            };
            console.log('Type changé - réinitialisation complète');
        } else {
            this.currentChecklist.type = selectedType;
            console.log('Même type - pas de réinitialisation');
        }

        switch (selectedType) {
            case 'modele':
                $('#modeleSelector').show();
                break;
            case 'copy':
                $('#prsSelector').show();
                // Charger la liste des PRS avec checklist
                this.loadPrsWithChecklistForSelector();
                break;
            case 'custom':
                this.showChecklistEditor();
                break;
        }

        console.log('currentChecklist après handleTypeChange:', JSON.stringify(this.currentChecklist));
        this.updateChecklistData();
        console.log('=== FIN handleTypeChange ===');
    }

    async loadChecklistModele(modeleId) {
        try {
            console.log('=== DEBUT loadChecklistModele ===');
            console.log('modeleId reçu:', modeleId);
            console.log('currentChecklist avant:', JSON.stringify(this.currentChecklist));

            this.showLoading('Chargement du modèle...');

            const response = await fetch(`/api/checklists/modeles/${modeleId}`);

            // ✅ Vérifiez le contenu de la réponse AVANT de parser le JSON
            const responseText = await response.text();
            console.log('Réponse brute:', responseText);

            if (!response.ok) {
                throw new Error(`Erreur HTTP: ${response.status} - ${responseText}`);
            }

            // ✅ Maintenant, parsez le JSON
            const modele = JSON.parse(responseText);
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

            this.currentChecklist.sourceId = parseInt(modeleId);

            console.log('currentChecklist après assignation sourceId:', JSON.stringify(this.currentChecklist));

            this.updateChecklistData();
            console.log('ChecklistData après updateChecklistData:', $('#checklistData').val());

            this.hideLoading();
            this.showChecklistEditor();
            this.renderChecklistItems();

            this.showNotification('Modèle de checklist appliqué avec succès', 'success');

            console.log('=== FIN loadChecklistModele ===');

        } catch (error) {
            console.error('Erreur lors du chargement du modèle:', error);
            this.hideLoading();
            this.showNotification('Erreur lors du chargement du modèle: ' + error.message, 'danger');
        }
    }

    // Fonction pour charger les PRS avec checklist dans le sélecteur
    async loadPrsWithChecklistForSelector() {
        try {
            const response = await fetch('/api/checklists/prs-with-checklist');
            if (!response.ok) throw new Error('Erreur lors du chargement des PRS');

            const prsWithChecklist = await response.json();
            const selector = document.getElementById('prsSourceSelect');

            // Vider le sélecteur
            selector.innerHTML = '<option value="">-- Sélectionner une PRS --</option>';

            // Ajouter les PRS avec checklist
            prsWithChecklist.forEach(prs => {
                const option = document.createElement('option');
                option.value = prs.id;
                option.textContent = `PRS-${prs.id} - ${prs.titre} (${prs.nombreElements} éléments, ${prs.pourcentageCompletion}% complété)`;
                selector.appendChild(option);
            });

            // Ajouter le gestionnaire d'événement pour la sélection
            selector.addEventListener('change', (e) => {
                const selectedPrsId = e.target.value;
                if (selectedPrsId) {
                    this.currentChecklist.sourceId = parseInt(selectedPrsId);
                    this.updateChecklistData();

                    // Optionnel : prévisualiser la checklist
                    this.loadChecklistFromPrs(selectedPrsId);
                } else {
                    this.currentChecklist.sourceId = null;
                    this.updateChecklistData();
                }
            });

        } catch (error) {
            console.error('Erreur:', error);
            const selector = document.getElementById('prsSourceSelect');
            selector.innerHTML = '<option value="">-- Erreur de chargement --</option>';
            this.showNotification('Erreur lors du chargement des PRS', 'danger');
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

            this.currentChecklist.elements = (data.checklist || []).map(item => ({
                id: ++this.elementIdCounter,
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
        console.log('Affichage de l\'éditeur de checklist');
        this.ensureEditorContent();
        $(this.options.containerSelector).show();
        this.updateItemCount();

        if (this.currentChecklist.type === 'custom' && this.currentChecklist.elements.length === 0) {
            this.addNewChecklistItem();
        }
    }

    ensureEditorContent() {
        const container = $(this.options.containerSelector);
        if (container.find('#checklistItems').length === 0) {
            this.hideLoading();
        }
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
        console.log('Contenu de l\'éditeur restauré, bouton ajouté');
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
        console.log('Ajout d\'un nouvel élément');

        const newElement = {
            id: ++this.elementIdCounter,
            categorie: '',
            sousCategorie: '',
            libelle: '',
            priorite: 3,
            delaiDefautJours: 1,
            obligatoire: false
        };

        this.currentChecklist.elements.unshift(newElement);
        this.renderChecklistItems();
        this.updateChecklistData();

        console.log('Élément ajouté, total:', this.currentChecklist.elements.length);

        $(this.options.itemsContainer).scrollTop(0);
        setTimeout(() => {
            $(this.options.itemsContainer).find('.checklist-item:first-child input[name="libelle"]').focus();
        }, 100);
    }

    removeChecklistItem(e) {
        const elementId = parseInt($(e.target).closest('.checklist-item').data('element-id'));
        console.log('Suppression de l\'élément avec ID:', elementId);

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
        const $item = $(e.target).closest('.checklist-item');
        const elementId = parseInt($item.data('element-id'));
        const field = $(e.target).attr('name');
        let value = $(e.target).val();

        if (field === 'obligatoire') {
            value = $(e.target).is(':checked');
        } else if (field === 'priorite' || field === 'delaiDefautJours') {
            value = parseInt(value) || (field === 'priorite' ? 3 : 1);
        }

        const element = this.currentChecklist.elements.find(el => el.id === elementId);
        if (element) {
            element[field] = value;
            this.updateChecklistData();
            this.updateItemCount();

            if (field === 'priorite') {
                const prioriteInfo = this.getPrioriteInfo(value);
                $item.find('.badge').first()
                    .css('background-color', prioriteInfo.color)
                    .text(prioriteInfo.label);
                $item.css('border-left-color', prioriteInfo.color);
            }

            if (field === 'delaiDefautJours') {
                $item.find('.text-muted').text(`Délai: ${value} jour(s)`);
            }

            if (field === 'obligatoire') {
                $item.find('.form-check-label').text(value ? 'Oui' : 'Non');
            }
        }
    }

    updateItemCount() {
        const count = this.currentChecklist.elements.length;
        $('#checklistItemCount').text(count);
    }

    // CORRECTION : Mise à jour des données avec vérification du formulaire
    updateChecklistData() {
        const checklistData = {
            type: this.currentChecklist.type,
            sourceId: this.currentChecklist.sourceId,
            elements: this.currentChecklist.elements.map(el => {
                const { id, ...elementWithoutId } = el;
                return elementWithoutId;
            })
        };

        // CORRECTION : Cibler spécifiquement le formulaire principal
        const $form = $(this.options.formSelector);
        let $hiddenField = $('#checklistData');

        if ($hiddenField.length === 0 || $hiddenField.closest('form')[0] !== $form[0]) {
            // Supprimer l'ancien champ s'il existe
            $hiddenField.remove();

            // Créer le nouveau champ dans le bon formulaire
            $hiddenField = $('<input type="hidden" id="checklistData" name="ChecklistData">');
            $form.append($hiddenField);
            console.log('Champ ChecklistData créé/recréé dans updateChecklistData');
        }

        const jsonData = JSON.stringify(checklistData);
        $hiddenField.val(jsonData);

        // Debug amélioré
        console.log('=== DEBUG CHECKLIST ===');
        console.log('Données checklist:', checklistData);
        console.log('JSON généré:', jsonData);
        console.log('Champ DOM:', $hiddenField[0]);
        console.log('Valeur dans le champ:', $hiddenField.val());
        console.log('Formulaire parent:', $hiddenField.closest('form')[0]);
        console.log('Formulaire cible:', $form[0]);
        console.log('Champ dans le bon formulaire:', $hiddenField.closest('form')[0] === $form[0]);
        console.log('========================');
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

        setTimeout(() => {
            notification.alert('close');
        }, 5000);
    }

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

        return { isValid: true, message: '' };
    }
}

// CORRECTION : Initialisation avec le bon sélecteur de formulaire
$(document).ready(() => {
    console.log('Initialisation du ChecklistManager');

    // Attendre un peu que le DOM soit complètement chargé
    setTimeout(() => {
        window.checklistManager = new ChecklistManager({
            formSelector: '#create-form' // CORRECTION : Sélecteur spécifique du formulaire
        });

        // Debug post-initialisation
        setTimeout(() => {
            console.log('Vérification post-initialisation:');
            console.log('- Instance manager:', window.checklistManager);
            console.log('- Formulaire principal:', $('#create-form')[0]);
            console.log('- Champ ChecklistData:', $('#checklistData').length > 0 ? 'Présent' : 'Absent');
            console.log('- Champ dans le formulaire:', $('#checklistData').closest('#create-form').length > 0 ? 'OUI' : 'NON');
            console.log('- Valeur initiale:', $('#checklistData').val());
        }, 500);
    }, 100);
});