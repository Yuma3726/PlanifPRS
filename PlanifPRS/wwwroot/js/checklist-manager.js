/**
 * Gestionnaire de checklist pour PlanifPRS
 * Permet de gérer les checklists des PRS lors de la création et modification
 */

class ChecklistManager {
    constructor(options = {}) {
        this.options = {
            containerSelector: '#checklistEditor',
            typeSelector: '#checklistTypeSelector',
            modeleSelector: '#checklistModele',
            searchInput: '#searchPrsInput',
            searchResults: '#prsSearchResults',
            itemsContainer: '#checklistItemsContainer',
            addButton: '#btnAddChecklistItem',
            dataField: '#checklistData',
            itemTemplate: '#checklistItemTemplate',
            ...options
        };

        this.currentChecklist = {
            type: null,
            sourceId: null,
            elements: []
        };

        this.categories = [
            'Produit',
            'Documentation',
            'Process',
            'Matière',
            'Production'
        ];

        this.init();
    }

    init() {
        this.bindEvents();
        this.loadCategories();
    }

    bindEvents() {
        // Sélection du type de checklist
        $(this.options.typeSelector).on('change', (e) => {
            this.handleTypeChange(e.target.value);
        });

        // Sélection d'un modèle
        $(this.options.modeleSelector).on('change', (e) => {
            if (e.target.value) {
                this.loadChecklistModele(e.target.value);
            }
        });

        // Recherche de PRS
        $(this.options.searchInput).on('input', this.debounce((e) => {
            this.searchPrs(e.target.value);
        }, 300));

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
                throw new Error('Erreur lors du chargement du modèle');
            }

            const modele = await response.json();

            this.currentChecklist.elements = modele.elements.map(element => ({
                categorie: element.categorie,
                sousCategorie: element.sousCategorie || '',
                libelle: element.libelle,
                ordre: element.ordre,
                obligatoire: element.obligatoire
            }));

            this.currentChecklist.sourceId = modeleId;

            this.showChecklistEditor();
            this.showNotification('Modèle de checklist appliqué avec succès', 'success');

        } catch (error) {
            console.error('Erreur:', error);
            this.showNotification('Erreur lors du chargement du modèle', 'danger');
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
                            onclick="checklistManager.selectPrsForCopy(${prs.id}, '${prs.titre}', ${prs.nombreElementsChecklist})">
                        <div>
                            <div class="fw-bold">${prs.titre}</div>
                            <small class="text-muted">ID: ${prs.id} | Équipement: ${prs.equipement}</small>
                            <div class="mt-1">
                                <span class="badge ${badgeClass}">${prs.nombreElementsChecklist} éléments - ${prs.pourcentageCompletion}% complété</span>
                            </div>
                        </div>
                        <div>
                            <i class="fas fa-copy text-primary"></i>
                        </div>
                    </button>
                `;
            });
            html += '</div>';

            $(this.options.searchResults).html(html);

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

    async selectPrsForCopy(prsId, prsTitle, nombreElements) {
        if (nombreElements === 0) {
            this.showNotification('Cette PRS ne possède pas de checklist à copier', 'warning');
            return;
        }

        try {
            this.showLoading('Chargement de la checklist...');

            const response = await fetch(`/api/checklist/prs/${prsId}`);
            if (!response.ok) {
                throw new Error('Erreur lors du chargement de la checklist');
            }

            const data = await response.json();

            this.currentChecklist.elements = data.elements.map(element => ({
                categorie: element.categorie || 'Produit',
                sousCategorie: element.sousCategorie || '',
                libelle: element.libelle || element.tache,
                ordre: element.ordre,
                obligatoire: element.obligatoire
            }));

            this.currentChecklist.sourceId = prsId;

            this.showChecklistEditor();
            this.showNotification(`Checklist de "${prsTitle}" copiée avec succès`, 'success');

            $(this.options.searchResults).hide();

        } catch (error) {
            console.error('Erreur:', error);
            this.showNotification('Erreur lors de la copie de la checklist', 'danger');
        }
    }

    showChecklistEditor() {
        $(this.options.containerSelector).show();
        this.updateChecklistDisplay();
    }

    updateChecklistDisplay() {
        const container = $(this.options.itemsContainer);
        container.empty();

        // Grouper par catégorie
        const categorizedElements = {};
        this.currentChecklist.elements.forEach((element, index) => {
            const category = element.categorie || 'Autres';
            if (!categorizedElements[category]) {
                categorizedElements[category] = [];
            }
            categorizedElements[category].push({ ...element, index });
        });

        // Afficher par catégorie
        Object.keys(categorizedElements).sort().forEach(category => {
            const categoryDiv = $(`
                <div class="category-section mb-3">
                    <h6 class="category-header bg-primary text-white p-2 rounded d-flex align-items-center justify-content-between">
                        <span>
                            <i class="${this.getCategoryIcon(category)} me-2"></i>
                            ${category}
                        </span>
                        <span class="badge bg-light text-dark">${categorizedElements[category].length}</span>
                    </h6>
                    <div class="category-items"></div>
                </div>
            `);

            const categoryItems = categoryDiv.find('.category-items');
            categorizedElements[category].forEach(element => {
                categoryItems.append(this.createChecklistItemElement(element, element.index));
            });

            container.append(categoryDiv);
        });

        this.updateChecklistItemCount();
        this.updateChecklistData();
    }

    createChecklistItemElement(element, index) {
        const template = $(this.options.itemTemplate).html();
        const $item = $(template);

        $item.attr('data-index', index);

        // Remplir les valeurs
        $item.find('.checklist-categorie').val(element.categorie || '');
        $item.find('.checklist-souscategorie').val(element.sousCategorie || '');
        $item.find('.checklist-libelle').val(element.libelle || '');
        $item.find('.checklist-obligatoire').prop('checked', element.obligatoire || false);

        // Ajouter des classes CSS pour le style
        $item.addClass('fade-in');

        return $item;
    }

    addNewChecklistItem() {
        const newElement = {
            categorie: 'Produit',
            sousCategorie: '',
            libelle: '',
            ordre: this.currentChecklist.elements.length + 1,
            obligatoire: false
        };

        this.currentChecklist.elements.push(newElement);
        this.updateChecklistDisplay();

        // Scroll vers le nouvel élément
        setTimeout(() => {
            const lastItem = $(this.options.itemsContainer).find('.checklist-item').last();
            lastItem[0]?.scrollIntoView({ behavior: 'smooth', block: 'center' });
            lastItem.find('.checklist-libelle').focus();
        }, 100);
    }

    updateElementData(event) {
        const $item = $(event.target).closest('.checklist-item');
        const index = parseInt($item.attr('data-index'));
        const field = $(event.target).attr('data-field');
        let value = event.target.value;

        if (event.target.type === 'checkbox') {
            value = event.target.checked;
        }

        if (index >= 0 && index < this.currentChecklist.elements.length) {
            this.currentChecklist.elements[index][field] = value;
            this.updateChecklistData();

            // Si la catégorie change, mettre à jour l'affichage
            if (field === 'categorie') {
                setTimeout(() => this.updateChecklistDisplay(), 100);
            }
        }
    }

    removeChecklistItem(event) {
        const $item = $(event.target).closest('.checklist-item');
        const index = parseInt($item.attr('data-index'));

        if (confirm('Êtes-vous sûr de vouloir supprimer cet élément ?')) {
            this.currentChecklist.elements.splice(index, 1);

            // Animation de suppression
            $item.addClass('fade-out');
            setTimeout(() => {
                this.updateChecklistDisplay();
            }, 300);
        }
    }

    updateChecklistItemCount() {
        $('#checklistItemCount').text(this.currentChecklist.elements.length);
    }

    updateChecklistData() {
        $(this.options.dataField).val(JSON.stringify(this.currentChecklist));
    }

    async loadCategories() {
        try {
            const response = await fetch('/api/checklist/categories');
            if (response.ok) {
                const categories = await response.json();
                this.categories = categories.length > 0 ? categories : this.categories;
                this.updateCategorySelectors();
            }
        } catch (error) {
            console.warn('Impossible de charger les catégories dynamiques, utilisation des catégories par défaut');
        }
    }

    updateCategorySelectors() {
        const categorieOptions = this.categories.map(cat =>
            `<option value="${cat}">${cat}</option>`
        ).join('');

        // Mettre à jour le template
        const template = $(this.options.itemTemplate);
        template.find('.checklist-categorie').html(`
            <option value="">Catégorie</option>
            ${categorieOptions}
        `);
    }

    getCategoryIcon(category) {
        const icons = {
            'Produit': 'fas fa-box',
            'Documentation': 'fas fa-file-alt',
            'Process': 'fas fa-cogs',
            'Matière': 'fas fa-cubes',
            'Production': 'fas fa-industry'
        };
        return icons[category] || 'fas fa-check-circle';
    }

    showLoading(message = 'Chargement...') {
        $(this.options.containerSelector).html(`
            <div class="text-center py-4">
                <div class="spinner-border text-primary mb-3" role="status">
                    <span class="visually-hidden">${message}</span>
                </div>
                <div class="text-muted">${message}</div>
            </div>
        `);
        $(this.options.containerSelector).show();
    }

    showNotification(message, type = 'info') {
        // Utiliser le système de notification existant de Robia ou créer une notification simple
        if (typeof showRobiaNotification === 'function') {
            showRobiaNotification(message, type);
        } else {
            const alertClass = {
                'success': 'alert-success',
                'danger': 'alert-danger',
                'warning': 'alert-warning',
                'info': 'alert-info'
            }[type] || 'alert-info';

            const notification = $(`
                <div class="alert ${alertClass} alert-dismissible fade show position-fixed" 
                     style="top: 20px; right: 20px; z-index: 9999; min-width: 300px;">
                    <i class="fas fa-${type === 'success' ? 'check-circle' : type === 'danger' ? 'exclamation-triangle' : 'info-circle'} me-2"></i>
                    ${message}
                    <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
                </div>
            `);

            $('body').append(notification);

            // Auto-dismiss après 5 secondes
            setTimeout(() => {
                notification.alert('close');
            }, 5000);
        }
    }

    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Méthodes publiques pour l'interaction externe
    getChecklistData() {
        return this.currentChecklist;
    }

    resetChecklist() {
        this.currentChecklist = {
            type: null,
            sourceId: null,
            elements: []
        };
        $(this.options.typeSelector).val('');
        this.handleTypeChange('');
    }

    setChecklistData(data) {
        this.currentChecklist = data;
        this.updateChecklistDisplay();
    }

    validateChecklist() {
        const errors = [];

        if (this.currentChecklist.elements.length === 0) {
            errors.push('La checklist doit contenir au moins un élément');
        }

        this.currentChecklist.elements.forEach((element, index) => {
            if (!element.libelle || element.libelle.trim() === '') {
                errors.push(`L'élément ${index + 1} doit avoir un libellé`);
            }
            if (!element.categorie || element.categorie.trim() === '') {
                errors.push(`L'élément ${index + 1} doit avoir une catégorie`);
            }
        });

        return {
            isValid: errors.length === 0,
            errors: errors
        };
    }
}

// Initialisation globale
let checklistManager;

document.addEventListener('DOMContentLoaded', function () {
    // Initialiser le gestionnaire de checklist si les éléments sont présents
    if (document.querySelector('#checklistTypeSelector')) {
        checklistManager = new ChecklistManager();

        // Rendre accessible globalement pour les événements inline
        window.checklistManager = checklistManager;
    }
});

// CSS pour les animations (à ajouter dans un fichier CSS séparé)
const checklistStyles = `
.fade-in {
    animation: fadeIn 0.3s ease-in;
}

.fade-out {
    animation: fadeOut 0.3s ease-out;
}

@keyframes fadeIn {
    from { opacity: 0; transform: translateY(-10px); }
    to { opacity: 1; transform: translateY(0); }
}

@keyframes fadeOut {
    from { opacity: 1; transform: translateY(0); }
    to { opacity: 0; transform: translateY(-10px); }
}

.checklist-item {
    transition: all 0.3s ease;
}

.checklist-item:hover {
    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    transform: translateY(-1px);
}

.category-header {
    background: linear-gradient(135deg, #007bff 0%, #0056b3 100%) !important;
}
`;

// Ajouter les styles si pas déjà présents
if (!document.querySelector('#checklist-styles')) {
    const styleElement = document.createElement('style');
    styleElement.id = 'checklist-styles';
    styleElement.textContent = checklistStyles;
    document.head.appendChild(styleElement);
}