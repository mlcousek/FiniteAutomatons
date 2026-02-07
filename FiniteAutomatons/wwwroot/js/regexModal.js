// Regex to Automaton functionality with validation and preset handling
(function() {
    'use strict';

    const DEBOUNCE_DELAY = 300;
    let debounceTimer;

    function validateRegex(input) {
        if (!input) return { valid: false, error: 'Empty regex' };

        let openParens = 0;
        let openBrackets = 0;
        let escaped = false;

        for (let i = 0; i < input.length; i++) {
            const char = input[i];
            
            if (escaped) {
                escaped = false;
                continue;
            }

            if (char === '\\') {
                escaped = true;
                continue;
            }

            if (char === '(') openParens++;
            if (char === ')') openParens--;
            if (char === '[') openBrackets++;
            if (char === ']') openBrackets--;

            if (openParens < 0) return { valid: false, error: 'Mismatched parentheses' };
            if (openBrackets < 0) return { valid: false, error: 'Mismatched brackets' };
        }

        if (escaped) return { valid: false, error: 'Trailing escape character' };
        if (openParens !== 0) return { valid: false, error: 'Unclosed parentheses' };
        if (openBrackets !== 0) return { valid: false, error: 'Unclosed character class' };

        if (input.includes('[^')) {
            return { valid: false, error: 'Negated character classes [^] not supported' };
        }

        return { valid: true };
    }

    function showValidationFeedback(feedbackElement, message, isError) {
        if (!feedbackElement) return;

        if (message) {
            feedbackElement.textContent = message;
            feedbackElement.className = isError ? 'regex-validation-feedback error' : 'regex-validation-feedback success';
            feedbackElement.style.display = 'block';
            feedbackElement.setAttribute('role', 'alert');
        } else {
            feedbackElement.style.display = 'none';
            feedbackElement.removeAttribute('role');
        }
    }

    function initializeRegexValidation() {
        const regexInput = document.getElementById('regexInput');
        const feedback = document.querySelector('.regex-validation-feedback');
        const submitBtn = document.getElementById('regexSubmitBtn');

        if (!regexInput || !feedback || !submitBtn) return;

        regexInput.addEventListener('input', function(e) {
            const value = e.target.value;

            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                if (!value) {
                    showValidationFeedback(feedback, null);
                    submitBtn.disabled = false;
                    return;
                }

                const result = validateRegex(value);
                if (!result.valid) {
                    showValidationFeedback(feedback, result.error, true);
                    submitBtn.disabled = true;
                } else {
                    showValidationFeedback(feedback, null);
                    submitBtn.disabled = false;
                }
            }, DEBOUNCE_DELAY);
        });
    }

    async function loadPresets() {
        const select = document.getElementById('regexPresetSelect');
        if (!select) return;

        try {
            const response = await fetch('/Regex/GetPresets');
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const presets = await response.json();
            
            presets.forEach(preset => {
                const option = document.createElement('option');
                option.value = preset.key;
                option.textContent = preset.displayName;
                option.dataset.description = preset.description;
                option.dataset.accept = JSON.stringify(preset.acceptExamples);
                option.dataset.reject = JSON.stringify(preset.rejectExamples);
                option.dataset.pattern = preset.pattern;
                select.appendChild(option);
            });
        } catch (err) {
            console.error('Failed to load regex presets:', err);
            if (window.showAlert) {
                window.showAlert('warning', 'Could not load preset examples');
            }
        }
    }

    function updatePresetInfo(selectedOption) {
        const infoBox = document.getElementById('regexPresetInfo');
        const descriptionEl = document.getElementById('regexPresetDescription');
        const acceptList = document.getElementById('regexAcceptList');
        const rejectList = document.getElementById('regexRejectList');
        const submitBtn = document.getElementById('regexPresetSubmitBtn');

        if (!selectedOption || !selectedOption.value) {
            if (infoBox) infoBox.style.display = 'none';
            if (submitBtn) submitBtn.disabled = true;
            return;
        }

        if (descriptionEl) {
            descriptionEl.textContent = selectedOption.dataset.description || '';
        }

        if (acceptList) {
            acceptList.innerHTML = '';
            const acceptExamples = JSON.parse(selectedOption.dataset.accept || '[]');
            acceptExamples.forEach(ex => {
                const li = document.createElement('li');
                li.textContent = ex === '' ? '(empty string)' : ex;
                acceptList.appendChild(li);
            });
        }

        if (rejectList) {
            rejectList.innerHTML = '';
            const rejectExamples = JSON.parse(selectedOption.dataset.reject || '[]');
            rejectExamples.forEach(ex => {
                const li = document.createElement('li');
                li.textContent = ex === '' ? '(empty string)' : ex;
                rejectList.appendChild(li);
            });
        }

        if (infoBox) {
            infoBox.style.display = 'block';
        }
        if (submitBtn) {
            submitBtn.disabled = false;
        }
    }

    function initializePresetSelection() {
        const select = document.getElementById('regexPresetSelect');
        if (!select) return;

        select.addEventListener('change', function() {
            const selectedOption = this.options[this.selectedIndex];
            updatePresetInfo(selectedOption);
        });
    }

    async function handleFormSubmit(form, successCallback) {
        const formData = new FormData(form);
        const submitBtn = form.querySelector('button[type="submit"]');
        
        if (submitBtn) {
            submitBtn.disabled = true;
            const originalText = submitBtn.innerHTML;
            submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Processing...';

            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();

                if (result.success) {
                    if (result.redirect) {
                        window.location.href = result.redirect;
                    } else if (successCallback) {
                        successCallback(result);
                    }
                } else {
                    if (window.showAlert) {
                        window.showAlert('error', result.error || 'Failed to convert regex');
                    } else {
                        alert(result.error || 'Failed to convert regex');
                    }
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalText;
                }
            } catch (err) {
                console.error('Form submission error:', err);
                if (window.showAlert) {
                    window.showAlert('error', 'Network error: ' + err.message);
                } else {
                    alert('Network error: ' + err.message);
                }
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalText;
            }
        }
    }

    function initializeFormSubmission() {
        const customForm = document.getElementById('regexCustomForm');
        const presetForm = document.getElementById('regexPresetForm');

        if (customForm) {
            customForm.addEventListener('submit', async function(e) {
                e.preventDefault();
                await handleFormSubmit(this);
            });
        }

        if (presetForm) {
            presetForm.addEventListener('submit', async function(e) {
                e.preventDefault();
                await handleFormSubmit(this);
            });
        }
    }

    function initialize() {
        initializeRegexValidation();
        initializePresetSelection();
        initializeFormSubmission();
        loadPresets();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize);
    } else {
        initialize();
    }
})();
