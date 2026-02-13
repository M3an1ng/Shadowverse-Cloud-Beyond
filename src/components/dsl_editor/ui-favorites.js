export function removeFavoritesMenu(editor) {
    const existingMenu = document.querySelector(".dsl-favorites-menu");
    if (existingMenu) {
        existingMenu.remove();
    }
    if (editor.favoriteMenuOutsideHandler) {
        document.removeEventListener("click", editor.favoriteMenuOutsideHandler);
    }
    clearTimeout(editor.favoriteSearchTimer);
    editor.favoriteMenuOutsideHandler = null;
    editor.favoriteMenuAnchor = null;
}

export function showFavoritesMenu(editor, anchor) {
    if (!anchor) return;

    const menu = document.createElement("div");
    menu.className = "dsl-favorites-menu";

    const closeMenu = () => {
        removeFavoritesMenu(editor);
    };

    renderFavoritesSection(editor, menu, closeMenu);

    const rect = anchor.getBoundingClientRect();
    const menuWidth = 320;
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const spaceBelow = viewportHeight - rect.bottom - 10;
    const spaceAbove = rect.top - 10;

    let maxMenuHeight = Math.min(420, Math.max(0, spaceBelow));
    let menuTop = rect.bottom + 4;
    if (spaceBelow < 180 && spaceAbove > spaceBelow) {
        maxMenuHeight = Math.min(420, Math.max(0, spaceAbove));
        menuTop = rect.top - maxMenuHeight - 4;
    }
    if (menuTop < 10) {
        menuTop = 10;
    }

    let menuLeft = rect.right - menuWidth;
    if (menuLeft < 10) {
        menuLeft = 10;
    }
    if (menuLeft + menuWidth > viewportWidth - 10) {
        menuLeft = viewportWidth - menuWidth - 10;
    }

    menu.style.cssText = `
        left: ${menuLeft}px;
        top: ${menuTop}px;
        width: ${menuWidth}px;
        max-height: ${maxMenuHeight}px;
    `;

    document.body.appendChild(menu);

    const outsideHandler = (event) => {
        if (!menu.contains(event.target) && event.target !== anchor) {
            closeMenu();
        }
    };

    editor.favoriteMenuOutsideHandler = outsideHandler;
    editor.favoriteMenuAnchor = anchor;

    setTimeout(() => document.addEventListener("click", outsideHandler), 0);
}

export function toggleFavoritesMenu(editor, anchor) {
    const existingMenu = document.querySelector(".dsl-favorites-menu");
    const isSameAnchor = editor.favoriteMenuAnchor === anchor;
    if (existingMenu) {
        removeFavoritesMenu(editor);
        if (isSameAnchor) {
            return;
        }
    }
    showFavoritesMenu(editor, anchor);
}

export function renderFavoritesToolbar(editor) {
    const toolbar = document.createElement("div");
    toolbar.className = "dsl-favorites-toolbar";

    const addWrapper = document.createElement("span");
    addWrapper.className = "dsl-favorites-toolbar__add";
    const addButton = document.createElement("button");
    addButton.type = "button";
    addButton.textContent = "收藏当前";
    addButton.className = "dsl-favorites-btn dsl-favorites-btn--add";
    if (!editor.userId) {
        addWrapper.title = "登录后可使用收藏";
        addButton.disabled = true;
        addButton.classList.add("is-disabled");
    }
    addButton.addEventListener("click", async (event) => {
        event.preventDefault();
        await editor.createFavoriteFromCurrentExpression();
    });
    addWrapper.appendChild(addButton);
    toolbar.appendChild(addWrapper);

    const listButton = document.createElement("button");
    listButton.type = "button";
    listButton.textContent = "收藏列表";
    listButton.className = "dsl-favorites-btn dsl-favorites-btn--list";
    listButton.addEventListener("click", (event) => {
        event.preventDefault();
        toggleFavoritesMenu(editor, listButton);
    });
    toolbar.appendChild(listButton);

    return toolbar;
}

export function renderFavoritesSection(editor, menu, closeMenu) {
    const section = document.createElement("div");
    section.className = "dsl-favorites-panel";

    const header = document.createElement("div");
    header.className = "dsl-favorites-panel__header";

    const title = document.createElement("div");
    title.textContent = "收藏";
    title.className = "dsl-favorites-panel__title";
    header.appendChild(title);

    const addWrapper = document.createElement("span");
    addWrapper.className = "dsl-favorites-panel__add";
    const addButton = document.createElement("button");
    addButton.type = "button";
    addButton.textContent = "收藏当前表达式";
    addButton.className = "dsl-favorites-btn dsl-favorites-btn--panel-add";
    addButton.disabled = !editor.userId;
    if (!editor.userId) {
        addWrapper.title = "登录后可使用收藏";
        addButton.classList.add("is-disabled");
    }
    addButton.addEventListener("click", async (event) => {
        event.stopPropagation();
        await editor.createFavoriteFromCurrentExpression();
        renderLists();
    });
    addWrapper.appendChild(addButton);
    header.appendChild(addWrapper);

    const searchInput = document.createElement("input");
    searchInput.type = "text";
    searchInput.placeholder = editor.userId ? "搜索收藏" : "登录后可使用收藏";
    searchInput.value = editor.favoriteState.query || "";
    searchInput.disabled = !editor.userId;
    searchInput.className = "dsl-favorites-search";

    const recentTitle = document.createElement("div");
    recentTitle.textContent = "最近使用";
    recentTitle.className = "dsl-favorites-section-title";

    const recentList = document.createElement("div");
    const favoritesTitle = document.createElement("div");
    favoritesTitle.textContent = "收藏列表";
    favoritesTitle.className = "dsl-favorites-section-title";

    const favoritesList = document.createElement("div");
    const pagination = document.createElement("div");
    pagination.style.cssText = "display:flex;align-items:center;justify-content:flex-end;gap:8px;margin-top:8px;";

    const prevPageBtn = document.createElement("button");
    prevPageBtn.type = "button";
    prevPageBtn.textContent = "上一页";
    prevPageBtn.className = "dsl-favorites-btn";
    prevPageBtn.style.cssText = "padding:4px 8px;font-size:12px;";

    const pageInfo = document.createElement("span");
    pageInfo.style.cssText = "font-size:12px;color:#6b7280;";

    const nextPageBtn = document.createElement("button");
    nextPageBtn.type = "button";
    nextPageBtn.textContent = "下一页";
    nextPageBtn.className = "dsl-favorites-btn";
    nextPageBtn.style.cssText = "padding:4px 8px;font-size:12px;";

    pagination.appendChild(prevPageBtn);
    pagination.appendChild(pageInfo);
    pagination.appendChild(nextPageBtn);

    const createStateLine = (container, message) => {
        const line = document.createElement("div");
        line.textContent = message;
        line.className = "dsl-favorites-state-line";
        container.appendChild(line);
    };

    const createFavoriteItem = (favorite, { showDelete = false } = {}) => {
        const favoriteId = favorite.id || favorite.Id;
        const item = document.createElement("div");
        item.className = "dsl-favorites-item";

        const topRow = document.createElement("div");
        topRow.className = "dsl-favorites-item__top";

        const label = document.createElement("div");
        label.textContent = favorite.label || favorite.Label || "(未命名)";
        label.className = "dsl-favorites-item__label";
        topRow.appendChild(label);

        if (showDelete) {
            const deleteBtn = document.createElement("button");
            deleteBtn.type = "button";
            deleteBtn.textContent = "删除";
            deleteBtn.className = "dsl-favorites-item__delete";
            deleteBtn.title = "删除收藏";
            deleteBtn.addEventListener("click", async (event) => {
                event.preventDefault();
                event.stopPropagation();
                if (!favoriteId) return;
                const confirmed = window.confirm("确定删除这个收藏吗？");
                if (!confirmed) return;
                const result = await editor.deleteFavorite(favoriteId);
                if (!result?.success) {
                    editor.showExpressionError?.("删除收藏失败，请稍后重试。");
                    return;
                }
                await editor.refreshFavorites(editor.favoriteState.query, editor.favoriteState.page);
                renderLists();
            });
            topRow.appendChild(deleteBtn);
        }
        item.appendChild(topRow);

        const desc = document.createElement("div");
        desc.textContent = favorite.expression || favorite.Expression || "";
        desc.className = "dsl-favorites-item__desc";
        item.appendChild(desc);
        item.addEventListener("click", () => {
            const expr = favorite.expression || favorite.Expression || "";
            if (expr) {
                editor.insertFavoriteExpression(expr);
                editor.markFavoriteUsed(favoriteId);
                closeMenu();
            }
        });

        return item;
    };

    const renderList = (container, items, emptyText, options = {}) => {
        container.innerHTML = "";
        if (!editor.userId) {
            createStateLine(container, "登录后可使用收藏功能。");
            return;
        }
        if (editor.favoriteState.loading) {
            createStateLine(container, "收藏加载中...");
            return;
        }
        if (editor.favoriteState.error) {
            createStateLine(container, editor.favoriteState.error);
            return;
        }
        if (!items || items.length === 0) {
            createStateLine(container, emptyText);
            return;
        }
        items.forEach(item => container.appendChild(createFavoriteItem(item, options)));
    };

    const renderLists = () => {
        const hasQuery = !!editor.favoriteState.query;
        recentTitle.style.display = hasQuery ? "none" : "block";
        recentList.style.display = hasQuery ? "none" : "block";
        renderList(recentList, editor.favoriteState.recentItems, "暂无最近使用。");
        renderList(favoritesList, editor.favoriteState.items, "暂无收藏。", { showDelete: true });
        renderPagination();
    };

    const renderPagination = () => {
        if (!editor.userId || editor.favoriteState.loading || editor.favoriteState.error) {
            pagination.style.display = "none";
            return;
        }

        const total = Math.max(0, Number(editor.favoriteState.total) || 0);
        const totalPages = Math.max(1, Number(editor.favoriteState.totalPages) || 1);
        const page = Math.max(1, Number(editor.favoriteState.page) || 1);
        const shouldShow = total > 0;

        pagination.style.display = shouldShow ? "flex" : "none";
        pageInfo.textContent = `${page} / ${totalPages} · 共 ${total} 条`;
        prevPageBtn.disabled = page <= 1;
        nextPageBtn.disabled = page >= totalPages;
    };

    prevPageBtn.addEventListener("click", async (event) => {
        event.preventDefault();
        event.stopPropagation();
        if (editor.favoriteState.loading) return;
        const targetPage = Math.max(1, (editor.favoriteState.page || 1) - 1);
        if (targetPage === editor.favoriteState.page) return;
        await editor.refreshFavorites(editor.favoriteState.query, targetPage);
        renderLists();
    });

    nextPageBtn.addEventListener("click", async (event) => {
        event.preventDefault();
        event.stopPropagation();
        if (editor.favoriteState.loading) return;
        const totalPages = Math.max(1, Number(editor.favoriteState.totalPages) || 1);
        const targetPage = Math.min(totalPages, (editor.favoriteState.page || 1) + 1);
        if (targetPage === editor.favoriteState.page) return;
        await editor.refreshFavorites(editor.favoriteState.query, targetPage);
        renderLists();
    });

    searchInput.addEventListener("input", () => {
        const value = searchInput.value.trim();
        clearTimeout(editor.favoriteSearchTimer);
        editor.favoriteSearchTimer = setTimeout(async () => {
            await editor.refreshFavorites(value, 1);
            renderLists();
        }, 300);
    });

    section.appendChild(header);
    section.appendChild(searchInput);
    section.appendChild(recentTitle);
    section.appendChild(recentList);
    section.appendChild(favoritesTitle);
    section.appendChild(favoritesList);
    section.appendChild(pagination);

    menu.appendChild(section);

    if (editor.userId) {
        editor.refreshFavorites(editor.favoriteState.query, editor.favoriteState.page || 1).then(renderLists);
    } else {
        renderLists();
    }
}
