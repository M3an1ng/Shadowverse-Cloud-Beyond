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

    console.log(
        "DslEditor: Showing options menu with",
        options.length,
        "options:",
        options
    );

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
                border-bottom: 1px solid #e9ecef;
                background: #ffffff;
                transition: background-color 0.15s ease;
            `;

            const label = document.createElement("span");
            label.className = "dsl-option-label";
            label.textContent = option.label;
            label.style.cssText = `
                font-weight: 600;
                color: #1a1a1a;
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
                    color: #4a5568;
                    margin-top: 3px;
                    line-height: 1.3;
                `;
                item.appendChild(description);
            }

            item.addEventListener("mouseenter", () => {
                item.style.background = "#2196f3";
                label.style.color = "#ffffff";
                if (option.description) {
                    const desc = item.querySelector('.dsl-option-description');
                    if (desc) desc.style.color = "#e3f2fd";
                }
            });

            item.addEventListener("mouseleave", () => {
                item.style.background = "#ffffff";
                label.style.color = "#1a1a1a";
                if (option.description) {
                    const desc = item.querySelector('.dsl-option-description');
                    if (desc) desc.style.color = "#4a5568";
                }
            });

            item.addEventListener("click", () => {
                console.log("DslEditor: Option selected:", option);
                editor.addNode(option);
                removeMenu();
            });

            return item;
        };

        const groupedOptions = editor.groupOptionsByType(options);
        console.log('DslEditor: Grouped options:', groupedOptions);

        Object.entries(groupedOptions).forEach(([groupName, groupOptions]) => {
            console.log(`DslEditor: Rendering group "${groupName}" with ${groupOptions.length} options:`, groupOptions);
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
                    border-bottom: 1px solid #e9ecef;
                    background: #f8f9fa;
                    font-weight: 600;
                    color: #495057;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    transition: background-color 0.15s ease;
                    font-size: 14px;
                `;

                const groupLabel = document.createElement("span");
                groupLabel.textContent = groupName;
                groupItem.appendChild(groupLabel);

                const arrow = document.createElement("span");
                arrow.textContent = "▶";
                arrow.style.cssText = "font-size: 12px; color: #495057; margin-left: 8px;";
                groupItem.appendChild(arrow);

                groupItem.addEventListener("mouseenter", () => {
                    groupItem.style.background = "#e9ecef";
                });

                groupItem.addEventListener("mouseleave", () => {
                    groupItem.style.background = "#f8f9fa";
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
        background: #ffffff;
        border: 1px solid #bdc3c7;
        border-radius: 6px;
        box-shadow: 0 6px 16px rgba(0,0,0,0.2);
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
        background: #ffffff;
        border: 1px solid #bdc3c7;
        border-radius: 6px;
        box-shadow: 0 6px 16px rgba(0,0,0,0.2);
        z-index: 1002;
        padding: 6px 0;
        font-size: 14px;
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    `;

    options.forEach((option) => {
        const item = document.createElement("div");
        item.className = "dsl-option";
        item.style.cssText = `
            padding: 10px 16px;
            cursor: pointer;
            border-bottom: none;
            background: #ffffff;
            transition: background-color 0.15s ease;
            border-radius: 3px;
            margin: 2px 4px;
        `;

        const label = document.createElement("span");
        label.className = "dsl-option-label";
        label.textContent = option.label;
        label.style.cssText = `
            font-weight: 500;
            color: #1a1a1a;
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
                color: #4a5568;
                margin-top: 3px;
                line-height: 1.3;
            `;
            item.appendChild(description);
        }

        item.addEventListener("mouseenter", () => {
            item.style.background = "#2196f3";
            item.style.borderRadius = "4px";
            label.style.color = "#ffffff";
            if (option.description) {
                const desc = item.querySelector('.dsl-option-description');
                if (desc) desc.style.color = "#e3f2fd";
            }
        });

        item.addEventListener("mouseleave", () => {
            item.style.background = "#ffffff";
            label.style.color = "#1a1a1a";
            if (option.description) {
                const desc = item.querySelector('.dsl-option-description');
                if (desc) desc.style.color = "#4a5568";
            }
        });

        item.addEventListener("click", () => {
            console.log("DslEditor: Option selected:", option);
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
            background: #f1f1f1;
            border-radius: 4px;
        }

        .${uniqueClass}::-webkit-scrollbar-thumb {
            background: #c1c1c1;
            border-radius: 4px;
            transition: background 0.2s ease;
        }

        .${uniqueClass}::-webkit-scrollbar-thumb:hover {
            background: #a1a1a1;
        }

        .${uniqueClass} {
            scrollbar-width: thin;
            scrollbar-color: #c1c1c1 #f1f1f1;
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
