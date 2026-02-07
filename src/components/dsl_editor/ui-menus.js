export function showAddNodeMenu(editor, button) {
    const existingMenu = document.querySelector(".dsl-add-menu");
    if (existingMenu) {
        existingMenu.remove();
    }

    const menu = document.createElement("div");
    menu.className = "dsl-options-dropdown";

    const removeMenu = () => {
        const groupItems = menu.querySelectorAll('.dsl-option-group');
        groupItems.forEach(item => {
            if (item._submenuCleanup) {
                item._submenuCleanup();
            }
        });

        document.querySelectorAll('.dsl-submenu').forEach(submenu => {
            submenu.remove();
        });

        menu.remove();
        document.removeEventListener("click", closeMenu);
    };

    const closeMenu = (e) => {
        if (!menu.contains(e.target) && e.target !== button) {
            removeMenu();
        }
    };

    const options = editor.getCurrentEditingOptions() || [];


    if (options.length === 0) {
        const noOptions = document.createElement("div");
        noOptions.className = "dsl-option";
        noOptions.innerHTML =
            '<span class="dsl-option-label">暂无可用选项</span>';
        menu.appendChild(noOptions);
    } else {
        const createDirectOptionItem = (option) => {
            const item = document.createElement("div");
            item.className = "dsl-option";
            item.style.cssText = `
                padding: 12px 16px;
                cursor: pointer;
                border-bottom: 1px solid #2c3e52;
                background: #223041;
                transition: background-color 0.15s ease, color 0.15s ease;
            `;

            const label = document.createElement("span");
            label.className = "dsl-option-label";
            label.textContent = option.label;
            label.style.cssText = `
                font-weight: 600;
                color: #e2e8f0;
                font-size: 14px;
                display: block;
            `;
            item.appendChild(label);

            if (option.description) {
                const description = document.createElement("div");
                description.className = "dsl-option-description";
                description.textContent = option.description;
                description.style.cssText = `
                    font-size: 12px;
                    color: #9fb3c8;
                    margin-top: 3px;
                    line-height: 1.3;
                `;
                item.appendChild(description);
            }

            item.addEventListener("mouseenter", () => {
                item.style.background = "#2f4b6c";
                label.style.color = "#f8fafc";
                if (option.description) {
                    const desc = item.querySelector('.dsl-option-description');
                    if (desc) desc.style.color = "#dbeafe";
                }
            });

            item.addEventListener("mouseleave", () => {
                item.style.background = "#223041";
                label.style.color = "#e2e8f0";
                if (option.description) {
                    const desc = item.querySelector('.dsl-option-description');
                    if (desc) desc.style.color = "#9fb3c8";
                }
            });

            item.addEventListener("click", () => {
                editor.addNode(option);
                removeMenu();
            });

            return item;
        };

        const groupedOptions = editor.groupOptionsByType(options);

        Object.entries(groupedOptions).forEach(([groupName, groupOptions]) => {
            const shouldRenderDirect = groupOptions.length === 1 && groupOptions[0].type === "listPipeline";
            if (shouldRenderDirect) {
                menu.appendChild(createDirectOptionItem(groupOptions[0]));
                return;
            }
            if (groupOptions.length > 0) {
                const groupItem = document.createElement("div");
                groupItem.className = "dsl-option-group";
                groupItem.style.cssText = `
                    position: relative;
                    padding: 12px 16px;
                    cursor: pointer;
                    border-bottom: 1px solid #2c3e52;
                    background: #1b2a3b;
                    font-weight: 600;
                    color: #e2e8f0;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    transition: background-color 0.15s ease, color 0.15s ease;
                    font-size: 14px;
                `;

                const groupLabel = document.createElement("span");
                groupLabel.textContent = groupName;
                groupItem.appendChild(groupLabel);

                const arrow = document.createElement("span");
                arrow.textContent = "▶";
                arrow.style.cssText = "font-size: 12px; color: #9fb3c8; margin-left: 8px;";
                groupItem.appendChild(arrow);

                groupItem.addEventListener("mouseenter", () => {
                    groupItem.style.background = "#2b3e57";
                    groupItem.style.color = "#f8fafc";
                    arrow.style.color = "#e2e8f0";
                });

                groupItem.addEventListener("mouseleave", () => {
                    groupItem.style.background = "#1b2a3b";
                    groupItem.style.color = "#e2e8f0";
                    arrow.style.color = "#9fb3c8";
                });

                let hoverTimeout;
                let currentSubmenu = null;

                const showSubmenu = () => {
                    clearTimeout(hoverTimeout);

                    if (currentSubmenu) {
                        currentSubmenu.remove();
                    }

                    currentSubmenu = createSubmenu(editor, groupOptions, groupItem, menu);
                    document.body.appendChild(currentSubmenu);

                    currentSubmenu.addEventListener("mouseenter", () => {
                        clearTimeout(hoverTimeout);
                    });

                    currentSubmenu.addEventListener("mouseleave", () => {
                        hoverTimeout = setTimeout(() => {
                            if (currentSubmenu) {
                                currentSubmenu.remove();
                                currentSubmenu = null;
                            }
                        }, 300);
                    });
                };

                const hideSubmenu = () => {
                    hoverTimeout = setTimeout(() => {
                        if (currentSubmenu) {
                            currentSubmenu.remove();
                            currentSubmenu = null;
                        }
                    }, 300);
                };

                groupItem.addEventListener("mouseenter", showSubmenu);
                groupItem.addEventListener("mouseleave", hideSubmenu);

                groupItem._submenuCleanup = () => {
                    clearTimeout(hoverTimeout);
                    if (currentSubmenu) {
                        currentSubmenu.remove();
                        currentSubmenu = null;
                    }
                };
                menu.appendChild(groupItem);
            }
        });
    }

    const rect = button.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const viewportWidth = window.innerWidth;

    let menuTop = rect.bottom + 5;
    let menuLeft = rect.left;
    const menuWidth = 250;
    const maxMenuHeight = Math.min(400, viewportHeight - rect.bottom - 20);

    if (menuLeft + menuWidth > viewportWidth) {
        menuLeft = viewportWidth - menuWidth - 10;
    }

    if (menuTop + maxMenuHeight > viewportHeight) {
        menuTop = rect.top - maxMenuHeight - 5;
        if (menuTop < 10) {
            menuTop = 10;
        }
    }

    menu.style.cssText = `
        position: fixed;
        left: ${menuLeft}px;
        top: ${menuTop}px;
        z-index: 1000;
        max-height: ${maxMenuHeight}px;
        overflow-y: auto;
        overflow-x: hidden;
        background: #1b2a3b;
        border: 1px solid #2c3e52;
        border-radius: 6px;
        box-shadow: 0 8px 20px rgba(0,0,0,0.35);
        min-width: 200px;
        max-width: 300px;
    `;

    addScrollbarStyles(editor, menu);

    document.body.appendChild(menu);

    setTimeout(() => document.addEventListener("click", closeMenu), 0);
}

export function createSubmenu(editor, options, parentItem, mainMenu) {
    const submenu = document.createElement("div");
    submenu.className = "dsl-submenu";

    const parentRect = parentItem.getBoundingClientRect();
    const mainMenuRect = mainMenu.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const viewportWidth = window.innerWidth;

    let submenuLeft = mainMenuRect.right + 5;
    let submenuTop = parentRect.top;
    const submenuWidth = 250;
    const maxSubmenuHeight = Math.min(350, viewportHeight - parentRect.top - 20);

    if (submenuLeft + submenuWidth > viewportWidth) {
        submenuLeft = mainMenuRect.left - submenuWidth - 5;
        if (submenuLeft < 10) {
            submenuLeft = 10;
        }
    }

    if (submenuTop + maxSubmenuHeight > viewportHeight) {
        submenuTop = viewportHeight - maxSubmenuHeight - 10;
        if (submenuTop < 10) {
            submenuTop = 10;
        }
    }

    submenu.style.cssText = `
        position: fixed;
        left: ${submenuLeft}px;
        top: ${submenuTop}px;
        min-width: 200px;
        max-width: 300px;
        max-height: ${maxSubmenuHeight}px;
        overflow-y: auto;
        overflow-x: hidden;
        background: #1b2a3b;
        border: 1px solid #2c3e52;
        border-radius: 6px;
        box-shadow: 0 8px 20px rgba(0,0,0,0.35);
        z-index: 1002;
        padding: 6px 0;
        font-size: 14px;
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    `;

    options.forEach((option) => {
        const item = document.createElement("div");
        item.className = "dsl-option";
        item.style.cssText = `
            padding: 10px 16px;
            cursor: pointer;
            border-bottom: none;
            background: #223041;
            transition: background-color 0.15s ease, color 0.15s ease;
            border-radius: 4px;
            margin: 2px 4px;
        `;

        const label = document.createElement("span");
        label.className = "dsl-option-label";
        label.textContent = option.label;
        label.style.cssText = `
            font-weight: 500;
            color: #e2e8f0;
            font-size: 14px;
            display: block;
        `;
        item.appendChild(label);

        if (option.description) {
            const description = document.createElement("div");
            description.className = "dsl-option-description";
            description.textContent = option.description;
            description.style.cssText = `
                font-size: 12px;
                color: #9fb3c8;
                margin-top: 3px;
                line-height: 1.3;
            `;
            item.appendChild(description);
        }

        item.addEventListener("mouseenter", () => {
            item.style.background = "#2f4b6c";
            item.style.borderRadius = "4px";
            label.style.color = "#f8fafc";
            if (option.description) {
                const desc = item.querySelector('.dsl-option-description');
                if (desc) desc.style.color = "#dbeafe";
            }
        });

        item.addEventListener("mouseleave", () => {
            item.style.background = "#223041";
            label.style.color = "#e2e8f0";
            if (option.description) {
                const desc = item.querySelector('.dsl-option-description');
                if (desc) desc.style.color = "#9fb3c8";
            }
        });

        item.addEventListener("click", () => {
            editor.addNode(option);
            mainMenu.remove();
            submenu.remove();
        });

        submenu.appendChild(item);
    });

    addScrollbarStyles(editor, submenu);

    return submenu;
}

export function addScrollbarStyles(editor, element) {
    const style = document.createElement('style');
    const uniqueClass = `dsl-menu-${Date.now()}-${Math.random().toString(36).substring(2, 11)}`;
    element.classList.add(uniqueClass);

    style.textContent = `
        .${uniqueClass}::-webkit-scrollbar {
            width: 8px;
        }

        .${uniqueClass}::-webkit-scrollbar-track {
            background: #1b2a3b;
            border-radius: 4px;
        }

        .${uniqueClass}::-webkit-scrollbar-thumb {
            background: #3a4b60;
            border-radius: 4px;
            transition: background 0.2s ease;
        }

        .${uniqueClass}::-webkit-scrollbar-thumb:hover {
            background: #4a607a;
        }

        .${uniqueClass} {
            scrollbar-width: thin;
            scrollbar-color: #3a4b60 #1b2a3b;
        }
    `;

    document.head.appendChild(style);

    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            mutation.removedNodes.forEach((node) => {
                if (node === element) {
                    style.remove();
                    observer.disconnect();
                }
            });
        });
    });

    observer.observe(document.body, { childList: true, subtree: true });
}
