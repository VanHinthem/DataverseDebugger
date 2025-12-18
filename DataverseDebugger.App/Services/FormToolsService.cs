namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Provides JavaScript snippets for Dynamics 365/Power Apps form utilities.
    /// </summary>
    /// <remarks>
    /// These scripts are injected into the WebView2 browser to provide debugging
    /// utilities similar to the Level Up extension.
    /// </remarks>
    public static class FormToolsService
    {
        /// <summary>
        /// JavaScript to show logical names for all fields on the form.
        /// </summary>
        /// <remarks>
        /// Adds the logical name below each field label for easy reference
        /// when writing plugin code.
        /// </remarks>
        public static string ShowLogicalNamesScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.ui) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return;
        }
        
        Xrm.Page.ui.controls.forEach(function(control) {
            try {
                var controlName = control.getName();
                if (!controlName) return;
                
                var controlElement = document.querySelector('[data-id=""' + controlName + '""]') ||
                                   document.querySelector('[data-control-name=""' + controlName + '""]');
                
                if (controlElement) {
                    var label = controlElement.querySelector('label');
                    if (label && !label.querySelector('.dvdebug-logicalname')) {
                        var span = document.createElement('span');
                        span.className = 'dvdebug-logicalname';
                        span.style.cssText = 'display:block;font-size:10px;color:#1976D2;font-weight:normal;font-style:italic;';
                        span.textContent = controlName;
                        label.appendChild(span);
                    }
                }
            } catch (e) { }
        });
        
        // Also show tab names
        Xrm.Page.ui.tabs.forEach(function(tab) {
            try {
                var tabName = tab.getName();
                var tabElement = document.querySelector('[data-id=""' + tabName + '""]');
                if (tabElement) {
                    var label = tabElement.querySelector('span');
                    if (label && !label.querySelector('.dvdebug-logicalname')) {
                        var span = document.createElement('span');
                        span.className = 'dvdebug-logicalname';
                        span.style.cssText = 'margin-left:8px;font-size:10px;color:#1976D2;font-weight:normal;font-style:italic;';
                        span.textContent = '(' + tabName + ')';
                        label.appendChild(span);
                    }
                }
            } catch (e) { }
        });
        
        console.log('[DataverseDebugger] Logical names displayed');
    } catch (e) {
        console.error('[DataverseDebugger] Error showing logical names:', e);
    }
})();";

        /// <summary>
        /// JavaScript to hide logical names and restore original field labels.
        /// </summary>
        public static string ClearLogicalNamesScript => @"
(function() {
    try {
        var elements = document.querySelectorAll('.dvdebug-logicalname');
        elements.forEach(function(el) { el.remove(); });
        console.log('[DataverseDebugger] Logical names cleared');
    } catch (e) {
        console.error('[DataverseDebugger] Error clearing logical names:', e);
    }
})();";

        /// <summary>
        /// JavaScript to enable God Mode - makes all fields visible and editable.
        /// </summary>
        /// <remarks>
        /// Disables all field requirements, makes hidden fields visible,
        /// and unlocks read-only fields. Useful for testing.
        /// </remarks>
        public static string GodModeScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.ui) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return;
        }
        
        var count = { visible: 0, unlocked: 0, required: 0 };
        
        // Make all controls visible and enabled
        Xrm.Page.ui.controls.forEach(function(control) {
            try {
                if (control.setVisible) {
                    var wasHidden = !control.getVisible();
                    control.setVisible(true);
                    if (wasHidden) count.visible++;
                }
                if (control.setDisabled) {
                    var wasDisabled = control.getDisabled();
                    control.setDisabled(false);
                    if (wasDisabled) count.unlocked++;
                }
            } catch (e) { }
        });
        
        // Remove all required field constraints
        Xrm.Page.data.entity.attributes.forEach(function(attribute) {
            try {
                if (attribute.setRequiredLevel) {
                    var wasRequired = attribute.getRequiredLevel() !== 'none';
                    attribute.setRequiredLevel('none');
                    if (wasRequired) count.required++;
                }
            } catch (e) { }
        });
        
        // Show all tabs and sections
        Xrm.Page.ui.tabs.forEach(function(tab) {
            try {
                tab.setVisible(true);
                tab.sections.forEach(function(section) {
                    try { section.setVisible(true); } catch (e) { }
                });
            } catch (e) { }
        });
        
        console.log('[DataverseDebugger] God Mode enabled:', count);
        alert('God Mode enabled!\n\nMade visible: ' + count.visible + ' fields\nUnlocked: ' + count.unlocked + ' fields\nRemoved required: ' + count.required + ' fields');
    } catch (e) {
        console.error('[DataverseDebugger] Error enabling God Mode:', e);
        alert('Error enabling God Mode: ' + e.message);
    }
})();";

        /// <summary>
        /// JavaScript to highlight fields that have been modified on the form.
        /// </summary>
        public static string ShowChangedFieldsScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.data) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return;
        }
        
        // Clear previous highlights
        document.querySelectorAll('.dvdebug-changed').forEach(function(el) {
            el.classList.remove('dvdebug-changed');
            el.style.backgroundColor = '';
        });
        
        var changedFields = [];
        
        Xrm.Page.data.entity.attributes.forEach(function(attribute) {
            try {
                if (attribute.getIsDirty && attribute.getIsDirty()) {
                    var name = attribute.getName();
                    changedFields.push(name);
                    
                    var control = Xrm.Page.getControl(name);
                    if (control) {
                        var controlElement = document.querySelector('[data-id=""' + name + '""]') ||
                                           document.querySelector('[data-control-name=""' + name + '""]');
                        if (controlElement) {
                            controlElement.classList.add('dvdebug-changed');
                            controlElement.style.backgroundColor = '#FFF9C4';
                        }
                    }
                }
            } catch (e) { }
        });
        
        if (changedFields.length === 0) {
            alert('No fields have been modified.');
        } else {
            console.log('[DataverseDebugger] Changed fields:', changedFields);
            alert('Modified fields (' + changedFields.length + '):\n\n' + changedFields.join('\n'));
        }
    } catch (e) {
        console.error('[DataverseDebugger] Error showing changed fields:', e);
        alert('Error: ' + e.message);
    }
})();";

        /// <summary>
        /// JavaScript to copy the current record's ID (GUID) to clipboard.
        /// </summary>
        public static string CopyRecordIdScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.data || !Xrm.Page.data.entity) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return null;
        }
        
        var id = Xrm.Page.data.entity.getId();
        if (!id) {
            alert('Record ID not available (new record?)');
            return null;
        }
        
        // Remove braces if present
        id = id.replace(/[{}]/g, '');
        return id;
    } catch (e) {
        console.error('[DataverseDebugger] Error getting record ID:', e);
        return null;
    }
})();";

        /// <summary>
        /// JavaScript to copy the current record's URL to clipboard.
        /// </summary>
        public static string CopyRecordUrlScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.data || !Xrm.Page.data.entity) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return null;
        }
        
        var entityName = Xrm.Page.data.entity.getEntityName();
        var id = Xrm.Page.data.entity.getId();
        if (!id || !entityName) {
            alert('Record information not available');
            return null;
        }
        
        id = id.replace(/[{}]/g, '');
        var baseUrl = Xrm.Utility.getGlobalContext().getClientUrl();
        var url = baseUrl + '/main.aspx?etn=' + entityName + '&id=' + id + '&pagetype=entityrecord';
        return url;
    } catch (e) {
        console.error('[DataverseDebugger] Error getting record URL:', e);
        return null;
    }
})();";

        /// <summary>
        /// JavaScript to refresh the form without saving pending changes.
        /// </summary>
        public static string RefreshWithoutSaveScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.data) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return;
        }
        
        // Check for unsaved changes
        var isDirty = Xrm.Page.data.entity.getIsDirty();
        if (isDirty) {
            if (!confirm('There are unsaved changes. Discard and refresh?')) {
                return;
            }
        }
        
        // Refresh without save
        Xrm.Page.data.refresh(false).then(function() {
            console.log('[DataverseDebugger] Form refreshed without save');
        }).catch(function(err) {
            console.error('[DataverseDebugger] Refresh failed:', err);
            alert('Refresh failed: ' + err.message);
        });
    } catch (e) {
        console.error('[DataverseDebugger] Error refreshing:', e);
        alert('Error: ' + e.message);
    }
})();";

        /// <summary>
        /// JavaScript to display form properties and context information.
        /// </summary>
        public static string ShowFormInfoScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return;
        }
        
        var info = [];
        
        // Entity info
        if (Xrm.Page.data && Xrm.Page.data.entity) {
            info.push('=== Entity ===');
            info.push('Name: ' + Xrm.Page.data.entity.getEntityName());
            info.push('ID: ' + (Xrm.Page.data.entity.getId() || '(new record)').replace(/[{}]/g, ''));
            info.push('Primary Value: ' + (Xrm.Page.data.entity.getPrimaryAttributeValue() || '(none)'));
            info.push('Is Dirty: ' + Xrm.Page.data.entity.getIsDirty());
        }
        
        // Form info
        if (Xrm.Page.ui) {
            info.push('');
            info.push('=== Form ===');
            info.push('Form Type: ' + Xrm.Page.ui.getFormType() + ' (1=Create, 2=Update, 3=ReadOnly, 4=Disabled, 6=BulkEdit)');
            var formItem = Xrm.Page.ui.formSelector.getCurrentItem();
            if (formItem) {
                info.push('Form Name: ' + formItem.getLabel());
                info.push('Form ID: ' + formItem.getId());
            }
        }
        
        // User info
        if (Xrm.Utility && Xrm.Utility.getGlobalContext) {
            var ctx = Xrm.Utility.getGlobalContext();
            info.push('');
            info.push('=== User ===');
            info.push('User ID: ' + ctx.userSettings.userId.replace(/[{}]/g, ''));
            info.push('User Name: ' + ctx.userSettings.userName);
            info.push('Security Roles: ' + ctx.userSettings.securityRolePrivileges.length + ' privileges');
        }
        
        // Context info
        if (Xrm.Utility && Xrm.Utility.getGlobalContext) {
            var ctx = Xrm.Utility.getGlobalContext();
            info.push('');
            info.push('=== Environment ===');
            info.push('Org: ' + ctx.organizationSettings.uniqueName);
            info.push('URL: ' + ctx.getClientUrl());
            info.push('Version: ' + ctx.getVersion());
        }
        
        var message = info.join('\n');
        console.log('[DataverseDebugger] Form Info:\n' + message);
        alert(message);
    } catch (e) {
        console.error('[DataverseDebugger] Error getting form info:', e);
        alert('Error: ' + e.message);
    }
})();";

        /// <summary>
        /// JavaScript to open the Web API endpoint for the current record in a new tab.
        /// </summary>
        public static string OpenWebApiScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.data || !Xrm.Page.data.entity) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return;
        }
        
        var entityName = Xrm.Page.data.entity.getEntityName();
        var id = Xrm.Page.data.entity.getId();
        if (!id || !entityName) {
            alert('Record information not available');
            return;
        }
        
        id = id.replace(/[{}]/g, '');
        var baseUrl = Xrm.Utility.getGlobalContext().getClientUrl();
        
        // Get plural name (entity set name) - this is a best guess
        var entitySetName = entityName;
        if (entityName.endsWith('y')) {
            entitySetName = entityName.slice(0, -1) + 'ies';
        } else if (entityName.endsWith('s') || entityName.endsWith('x') || entityName.endsWith('ch') || entityName.endsWith('sh')) {
            entitySetName = entityName + 'es';
        } else {
            entitySetName = entityName + 's';
        }
        
        var url = baseUrl + '/api/data/v9.2/' + entitySetName + '(' + id + ')';
        window.open(url, '_blank');
        console.log('[DataverseDebugger] Opened Web API URL:', url);
    } catch (e) {
        console.error('[DataverseDebugger] Error opening Web API:', e);
        alert('Error: ' + e.message);
    }
})();";

        /// <summary>
        /// JavaScript to display all option set values on the form.
        /// </summary>
        public static string ShowOptionSetValuesScript => @"
(function() {
    try {
        if (typeof Xrm === 'undefined' || !Xrm.Page || !Xrm.Page.data) {
            alert('This page does not appear to be a Dynamics 365 form.');
            return;
        }
        
        var optionSets = [];
        
        Xrm.Page.data.entity.attributes.forEach(function(attribute) {
            try {
                var attrType = attribute.getAttributeType();
                if (attrType === 'optionset' || attrType === 'multiselectoptionset' || attrType === 'boolean') {
                    var name = attribute.getName();
                    var options = attribute.getOptions ? attribute.getOptions() : null;
                    var currentValue = attribute.getValue();
                    
                    var info = name + ' (current: ' + currentValue + ')';
                    if (options && options.length > 0) {
                        info += ':\n';
                        options.forEach(function(opt) {
                            var marker = (opt.value === currentValue) ? ' <- ' : '   ';
                            info += marker + opt.value + ' = ' + opt.text + '\n';
                        });
                    }
                    optionSets.push(info);
                }
            } catch (e) { }
        });
        
        if (optionSets.length === 0) {
            alert('No option set fields found on this form.');
        } else {
            var message = 'Option Set Fields:\n\n' + optionSets.join('\n');
            console.log('[DataverseDebugger] Option Sets:', optionSets);
            alert(message);
        }
    } catch (e) {
        console.error('[DataverseDebugger] Error showing option sets:', e);
        alert('Error: ' + e.message);
    }
})();";
    }
}
