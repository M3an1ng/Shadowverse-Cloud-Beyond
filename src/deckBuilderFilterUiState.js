export function applyDeckBuilderFilterUiState(root, filters = {}) {
    if (!root || typeof root.querySelector !== "function") {
        return;
    }

    const searchInput = root.querySelector("#card-search-input");
    if (searchInput) {
        searchInput.value = String(filters.search ?? "");
    }

    const activeCost = Number.isFinite(filters.cost) ? filters.cost : null;
    root.querySelectorAll(".cost-btn").forEach((button) => {
        const buttonCost = Number.parseInt(button.dataset.cost ?? "", 10);
        button.classList.toggle("active", Number.isFinite(buttonCost) && buttonCost === activeCost);
    });
}
