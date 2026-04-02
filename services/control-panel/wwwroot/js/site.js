document.addEventListener("DOMContentLoaded", function () {
    var mapCheckboxes = Array.from(document.querySelectorAll("input[name='Input.SelectedMaps']"));
    var startMapSelect = document.querySelector("[data-start-map-select='true']");
    var passwordToggleButtons = document.querySelectorAll("[data-password-toggle]");
    var savedSecretToggleButtons = document.querySelectorAll("[data-secret-toggle]");

    passwordToggleButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            var fieldId = button.getAttribute("data-password-toggle");
            var input = fieldId ? document.getElementById(fieldId) : null;
            if (!input) {
                return;
            }

            var nextType = input.getAttribute("type") === "password" ? "text" : "password";
            input.setAttribute("type", nextType);

            var isVisible = nextType === "text";
            button.classList.toggle("is-active", isVisible);

            var baseLabel = fieldId === "rcon-password" ? "RCON password" : "join password";
            button.setAttribute("aria-label", isVisible ? "Hide " + baseLabel : "Show " + baseLabel);
            button.setAttribute("title", isVisible ? "Hide " + baseLabel : "Show " + baseLabel);
        });
    });

    savedSecretToggleButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            var secretId = button.getAttribute("data-secret-toggle");
            var output = secretId ? document.querySelector("[data-secret-text='" + secretId + "']") : null;
            if (!output) {
                return;
            }

            var maskedValue = output.getAttribute("data-secret-masked") || "";
            var revealedValue = output.getAttribute("data-secret-revealed") || "";
            var isRevealed = output.textContent === revealedValue && revealedValue.length > 0;
            var nextValue = isRevealed ? maskedValue : revealedValue;
            output.textContent = nextValue;

            button.classList.toggle("is-active", !isRevealed);

            var baseLabel = secretId === "rcon-saved" ? "saved RCON password" : "saved join password";
            button.setAttribute("aria-label", isRevealed ? "Show " + baseLabel : "Hide " + baseLabel);
            button.setAttribute("title", isRevealed ? "Show " + baseLabel : "Hide " + baseLabel);
        });
    });

    var restartButtons = document.querySelectorAll("[data-confirm-restart='true']");
    restartButtons.forEach(function (button) {
        button.addEventListener("click", function (event) {
            if (!window.confirm("Save the settings and request a server restart?")) {
                event.preventDefault();
            }
        });
    });

    function normalizeChoiceFilterValue(text) {
        return (text || "").trim().toLowerCase();
    }

    function readChoiceFilterValues(element) {
        return (element.getAttribute("data-choice-filter-values") || "")
            .split(/\s+/)
            .map(normalizeChoiceFilterValue)
            .filter(Boolean);
    }

    function toggleChoiceAvailability(element, isAvailable) {
        element.disabled = !isAvailable;

        if (element instanceof HTMLInputElement && !isAvailable) {
            element.checked = false;
        }

        var decoratedParent = element.closest(".map-check, .rule-toggle, label");
        if (decoratedParent) {
            decoratedParent.classList.toggle("is-disabled", !isAvailable);
            decoratedParent.setAttribute("aria-disabled", isAvailable ? "false" : "true");
        }
    }

    function syncFilteredSelect(select, preferredValue) {
        var previousValue = select.value;
        var enabledOptions = Array.from(select.options).filter(function (option) {
            return !option.disabled;
        });

        if (enabledOptions.length === 0) {
            select.disabled = true;
            return;
        }

        select.disabled = false;

        var nextValue = enabledOptions.some(function (option) {
            return option.value === previousValue;
        })
            ? previousValue
            : "";

        if (!nextValue && preferredValue) {
            var preferredOption = enabledOptions.find(function (option) {
                return option.value === preferredValue;
            });
            nextValue = preferredOption ? preferredOption.value : "";
        }

        if (!nextValue) {
            nextValue = enabledOptions[0].value;
        }

        select.value = nextValue;
    }

    function bindChoiceFilters() {
        document.querySelectorAll("[data-choice-filter-scope='true']").forEach(function (scope) {
            var sourceSelect = scope.querySelector("[data-choice-filter-source='true']");
            if (!sourceSelect) {
                return;
            }

            var sourceOptions = Array.from(sourceSelect.options).map(function (option) {
                return {
                    value: option.value,
                    preferredValue: option.getAttribute("data-choice-filter-preferred-value") || ""
                };
            });

            var filterableElements = Array.from(scope.querySelectorAll("[data-choice-filter-values]"));
            var targetSelects = Array.from(scope.querySelectorAll("select[data-choice-filter-target-select='true']"));

            function syncChoiceFilterScope() {
                var selectedSourceValue = normalizeChoiceFilterValue(sourceSelect.value);
                var currentSource = sourceOptions.find(function (option) {
                    return normalizeChoiceFilterValue(option.value) === selectedSourceValue;
                }) || null;

                filterableElements.forEach(function (element) {
                    var supportedValues = readChoiceFilterValues(element);
                    var isAvailable = supportedValues.length === 0 || supportedValues.indexOf(selectedSourceValue) !== -1;
                    toggleChoiceAvailability(element, isAvailable);
                });

                targetSelects.forEach(function (select) {
                    syncFilteredSelect(select, currentSource ? currentSource.preferredValue : "");
                });

                rebuildStartMapOptions();
            }

            sourceSelect.addEventListener("change", syncChoiceFilterScope);
            syncChoiceFilterScope();
        });
    }

    function rebuildStartMapOptions() {
        if (!startMapSelect) {
            return;
        }

        var previousValue = startMapSelect.value;
        var selectedMaps = mapCheckboxes
            .filter(function (checkbox) { return checkbox.checked; })
            .map(function (checkbox) {
                return {
                    value: checkbox.value,
                    label: checkbox.getAttribute("data-map-label") || checkbox.value.toUpperCase()
                };
            });

        startMapSelect.innerHTML = "";

        if (selectedMaps.length === 0) {
            var emptyOption = document.createElement("option");
            emptyOption.value = "";
            emptyOption.textContent = "Select maps in the pool first";
            startMapSelect.appendChild(emptyOption);
            startMapSelect.value = "";
            startMapSelect.disabled = true;
            return;
        }

        startMapSelect.disabled = false;

        selectedMaps.forEach(function (map) {
            var option = document.createElement("option");
            option.value = map.value;
            option.textContent = map.label;
            startMapSelect.appendChild(option);
        });

        var nextValue = selectedMaps.some(function (map) { return map.value === previousValue; })
            ? previousValue
            : selectedMaps[0].value;

        startMapSelect.value = nextValue;
    }

    if (startMapSelect) {
        mapCheckboxes.forEach(function (checkbox) {
            checkbox.addEventListener("change", rebuildStartMapOptions);
        });

        rebuildStartMapOptions();
        startMapSelect.addEventListener("change", function () {
            var relatedCheckbox = document.querySelector("input[name='Input.SelectedMaps'][value='" + startMapSelect.value + "']");
            if (relatedCheckbox) {
                relatedCheckbox.checked = true;
            }
        });
    }

    bindChoiceFilters();

    document.querySelectorAll("[data-map-group-select]").forEach(function (button) {
        button.addEventListener("click", function () {
            var group = button.getAttribute("data-map-group-select");
            document.querySelectorAll("input[data-map-group='" + group + "']").forEach(function (checkbox) {
                if (!checkbox.disabled) {
                    checkbox.checked = true;
                }
            });
            rebuildStartMapOptions();
        });
    });

    document.querySelectorAll("[data-map-group-clear]").forEach(function (button) {
        button.addEventListener("click", function () {
            var group = button.getAttribute("data-map-group-clear");
            document.querySelectorAll("input[data-map-group='" + group + "']").forEach(function (checkbox) {
                checkbox.checked = false;
            });
            rebuildStartMapOptions();
        });
    });
});
