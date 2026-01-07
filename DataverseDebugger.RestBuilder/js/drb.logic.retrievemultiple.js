// #region DRB.Logic.RetrieveMultiple
/**
 * Retrieve Multiple - Refresh Dropdown Logic
 * @param {string} logicValue Logic Value ("and", "or")
 * @param {string} controlName Control Name
 * @param {boolean} isGroupLogic Is Group Logic
*/
DRB.Logic.RetrieveMultiple.RefreshDropdownLogic = function (logicValue, controlName, isGroupLogic) {
    // if filterFields control name is like
    // cbx_fc_filterCriteria_filterFields
    // cbx_fc_filterCriteria_filterGroups_0_filterFields
    // cbx_fc_filterCriteria_filterGroups_1_filterGroups_5_filterFields

    // if filterGroups control name is like
    // cbx_fg_filterCriteria_filterGroups
    // cbx_fg_filterCriteria_filterGroups_0_filterGroups
    // cbx_fg_filterCriteria_filterGroups_0_filterGroups_2_filterGroups

    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    // from the control name navigate to the right path but paying attention to the id of the group
    var splittedControlName = controlName.split("_");
    // remove the first entries until we find "filterCriteria"
    while (splittedControlName.length > 0 && splittedControlName[0] !== "filterCriteria") { splittedControlName.shift(); }
    // "filterFields" or "filterGroups" is always the last, remove it
    if (splittedControlName.length > 0) { splittedControlName.pop(); }

    splittedControlName.forEach(function (path) {
        if (isNaN(parseInt(path))) {
            // not a number
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    if (isGroupLogic === false) {
        // filterFields
        // set the logic value to the metadata
        refMetadata.filterFieldsLogic = logicValue;
        refConfiguration.filterFieldsLogic = logicValue;
    } else {
        // filterGroups
        // set the logic value to the metadata
        refMetadata.filterGroupsLogic = logicValue;
        refConfiguration.filterGroupsLogic = logicValue;
        // refresh the middle spans
        var spanClass = splittedControlName.join("_") + "_" + "filterGroups";
        $("." + spanClass).each(function (i, span) { $(span).html(logicValue); });
    }
}
/**
 * Retrieve Multiple - Bind Columns Logic
 * @param {string} id Id
*/
DRB.Logic.RetrieveMultiple.BindColumnsLogic = function (id) {
    $("#" + id).on("change", function (e) {
        // get value and control name
        var logicValue = $(this).val();
        var controlName = $(this).attr('id');
        DRB.Logic.RetrieveMultiple.RefreshDropdownLogic(logicValue, controlName, false);
    });
}

/**
 * Retrieve Multiple - Bind Groups Logic
 * @param {string} id Id
*/
DRB.Logic.RetrieveMultiple.BindGroupsLogic = function (id) {
    $("#" + id).on("change", function (e) {
        // get value and control name
        var logicValue = $(this).val();
        var controlName = $(this).attr('id');
        DRB.Logic.RetrieveMultiple.RefreshDropdownLogic(logicValue, controlName, true);
    });
}

/**
 * Retrieve Multiple - Hide Previous Add Button
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.HidePreviousAddButton = function (metadataPath) {
    $("#" + DRB.DOM.FilterGroups.DivChoice.Id + metadataPath).hide();
}

/**
 * Retrieve Multiple - Show Previous Add Button
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.ShowPreviousAddButton = function (metadataPath) {
    var splittedMetadataPath = metadataPath.split("_");
    splittedMetadataPath.pop(); // remove last item (filterFields or filterGroups)
    var previousMetadataPath = splittedMetadataPath.join("_");
    $("#" + DRB.DOM.FilterGroups.DivChoice.Id + previousMetadataPath).show();
}

/**
 * Retrieve Multiple - Add Filter Group
 * @param {string} container Container
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.AddFilterGroup = function (container, domObject, metadataPath) {
    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    var splittedMetadataPath = metadataPath.split("_");
    splittedMetadataPath.forEach(function (path) {
        if (isNaN(parseInt(path))) {
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // this is the correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    var index = 0;
    if (refMetadata.length > 0) {
        var maxValue = -1;
        refMetadata.forEach(function (item) { if (item.Id > maxValue) { maxValue = item.Id; } });
        index = maxValue + 1;
    }

    refMetadata.push({ Id: index });
    refConfiguration.push({});

    var metadataPathIndex = metadataPath + "_" + index;


    if (refMetadata.length > 1) {
        // extract the logic value
        var logicValue = $("#" + DRB.DOM[domObject].DropdownLogic.Id + metadataPath).val();
        $("#" + container).append(DRB.UI.CreateSpan("", logicValue, null, metadataPath + " filterspan"));
        $("#" + DRB.DOM[domObject].DivLogic.Id + metadataPath).show();
    }

    $("#" + container).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].MainDiv.Id + metadataPathIndex, "mapping-container0"));

    // Div Choice
    $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPathIndex).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].DivChoice.Id + metadataPathIndex));
    // add the close button inside the Div Choice
    $("#" + DRB.DOM[domObject].DivChoice.Id + metadataPathIndex).append(DRB.UI.CreateCloseButton(DRB.Logic.RetrieveMultiple.RemoveFilterGroup, domObject, metadataPathIndex));
    $("#" + DRB.DOM[domObject].DivChoice.Id + metadataPathIndex).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].DivChoice.Id + metadataPathIndex + "_dropdown", "dropdown shortdropdown"));
    $("#" + DRB.DOM[domObject].DivChoice.Id + metadataPathIndex + "_dropdown").append(DRB.UI.CreateButton(DRB.DOM[domObject].ButtonChoice.Id + metadataPathIndex, DRB.DOM[domObject].ButtonChoice.Name, DRB.DOM[domObject].ButtonChoice.Class));
    $("#" + DRB.DOM[domObject].ButtonChoice.Id + metadataPathIndex).attr("data-toggle", "dropdown");
    $("#" + DRB.DOM[domObject].ButtonChoice.Id + metadataPathIndex).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].DivDropdownChoice.Id + metadataPathIndex, DRB.DOM[domObject].DivDropdownChoice.Class));
    $("#" + DRB.DOM[domObject].DivDropdownChoice.Id + metadataPathIndex).append(DRB.UI.CreateButton(DRB.DOM[domObject].ButtonChoiceColumns.Id + metadataPathIndex, DRB.DOM[domObject].ButtonChoiceColumns.Name, DRB.DOM[domObject].ButtonChoiceColumns.Class, DRB.Logic.RetrieveMultiple.AddManuallyFilterColumns, "FilterColumns", metadataPathIndex));
    $("#" + DRB.DOM[domObject].DivDropdownChoice.Id + metadataPathIndex).append(DRB.UI.CreateButton(DRB.DOM[domObject].ButtonChoiceGroups.Id + metadataPathIndex, DRB.DOM[domObject].ButtonChoiceGroups.Name, DRB.DOM[domObject].ButtonChoiceGroups.Class, DRB.Logic.RetrieveMultiple.AddManuallyFilterGroups, "FilterGroups", metadataPathIndex));
}

/**
 * Retrieve Multiple - Remove Filter Group
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.RemoveFilterGroup = function (domObject, metadataPath) {
    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    var splittedMetadataPath = metadataPath.split("_");
    var indexToRemove = parseInt(splittedMetadataPath.pop()); // last path is always a number
    // navigate to the deepest path, refConfiguration goes by the index and not by path when path is a number
    splittedMetadataPath.forEach(function (path) {
        if (isNaN(parseInt(path))) {
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // this is the correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    // remove the item from metadata and configuration
    for (var i = 0; i < refMetadata.length; i++) {
        if (refMetadata[i].Id === indexToRemove) {
            refMetadata.splice(i, 1);
            refConfiguration.splice(i, 1);

            if (i > 0) {
                // remove the previous span if is not the first element
                $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).prev().remove();
            }
            else {
                // first element, remove the span after
                $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).next().remove();
            }
            // remove from UI
            $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).remove();
            break;
        }
    }
    if (refMetadata.length < 2) {
        var previousMetadataPath = splittedMetadataPath.join("_"); // previously pop the index so the previousMetadataPath will be xyz_filterGroups
        if (refMetadata.length === 1) {
            $("#" + DRB.DOM[domObject].DivLogic.Id + previousMetadataPath).hide();
            // set the logic to AND
            $("#" + DRB.DOM[domObject].DropdownLogic.Id + previousMetadataPath).val(DRB.Settings.OptionsAndOr[0].Id).change();
        }

        if (refMetadata.length === 0) {
            DRB.Logic.RetrieveMultiple.RemoveFilterGroups(domObject, previousMetadataPath);
        }
    }
}

/**
 * Retrieve Multiple - Start Add Filter
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.StartAddFilter = function (domObjectGroups, domObjectColumns, metadataPath) {
    DRB.Logic.RetrieveMultiple.AddManuallyFilterGroups(domObjectGroups, metadataPath);
    DRB.Logic.RetrieveMultiple.AddManuallyFilterColumns(domObjectColumns, metadataPath + "_filterGroups_0");
}

/**
 * Retrieve Multiple - Add Manually Filter Groups
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.AddManuallyFilterGroups = function (domObject, metadataPath) {
    DRB.Logic.RetrieveMultiple.AddFilterGroups(domObject, metadataPath);
    DRB.Logic.RetrieveMultiple.AddFilterGroup(DRB.DOM[domObject].DivGroups.Id + metadataPath + "_filterGroups", domObject, metadataPath + "_filterGroups");
}

/**
 * Retrieve Multiple - Add Filter Groups
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.AddFilterGroups = function (domObject, metadataPath) {
    DRB.Logic.RetrieveMultiple.HidePreviousAddButton(metadataPath);
    var container = DRB.DOM.FilterBy.MainDiv.Id;
    if (metadataPath !== "filterCriteria") { container = DRB.DOM[domObject].MainDiv.Id + metadataPath; }

    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    // navigate to the deepest path, refConfiguration goes by the index and not by path when path is a number
    var splittedMetadataPath = metadataPath.split("_");
    splittedMetadataPath.forEach(function (path) {
        if (isNaN(parseInt(path))) {
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // this is the correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    refMetadata.filterType = "groups";
    refConfiguration.filterType = "groups";
    if (!refMetadata.hasOwnProperty("filterGroupsLogic")) { refMetadata.filterGroupsLogic = "and"; }
    if (!refConfiguration.hasOwnProperty("filterGroupsLogic")) { refConfiguration.filterGroupsLogic = "and"; }

    if (!refMetadata.hasOwnProperty("filterGroups")) { refMetadata.filterGroups = []; }
    if (!refConfiguration.hasOwnProperty("filterGroups")) { refConfiguration.filterGroups = []; }

    metadataPath += "_filterGroups";
    $("#" + container).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].DivGroups.Id + metadataPath));

    $("#" + container).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].DivOptions.Id + metadataPath));
    // add group button
    $("#" + DRB.DOM[domObject].DivOptions.Id + metadataPath).append(DRB.UI.CreateSpacer());
    $("#" + DRB.DOM[domObject].DivOptions.Id + metadataPath).append(DRB.UI.CreateButton(DRB.DOM[domObject].AddButton.Id + metadataPath, DRB.DOM[domObject].AddButton.Name, DRB.DOM[domObject].AddButton.Class, DRB.Logic.RetrieveMultiple.AddFilterGroup, DRB.DOM[domObject].DivGroups.Id + metadataPath, domObject, metadataPath));

    // add logic dropdown
    $("#" + DRB.DOM[domObject].DivOptions.Id + metadataPath).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].DivLogic.Id + metadataPath, DRB.DOM[domObject].DivLogic.Class));
    $("#" + DRB.DOM[domObject].DivLogic.Id + metadataPath).append(DRB.UI.CreateSpan(DRB.DOM[domObject].SpanLogic.Id + metadataPath, DRB.DOM[domObject].SpanLogic.Name));
    $("#" + DRB.DOM[domObject].DivLogic.Id + metadataPath).append(DRB.UI.CreateSimpleDropdown(DRB.DOM[domObject].DropdownLogic.Id + metadataPath));
    DRB.UI.FillDropdown(DRB.DOM[domObject].DropdownLogic.Id + metadataPath, DRB.DOM[domObject].DropdownLogic.Name, new DRB.Models.Records(DRB.Settings.OptionsAndOr).ToDropdown());
    DRB.Logic.RetrieveMultiple.BindGroupsLogic(DRB.DOM[domObject].DropdownLogic.Id + metadataPath);
    $("#" + DRB.DOM[domObject].DropdownLogic.Id + metadataPath).val(refMetadata.filterGroupsLogic).change();
    $("#" + DRB.DOM[domObject].DivLogic.Id + metadataPath).hide(); // hide filter logic by default
}

/**
 * Retrieve Multiple - Add Manually Filter Columns
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.AddManuallyFilterColumns = function (domObject, metadataPath) {
    DRB.Logic.RetrieveMultiple.AddFilterColumns(domObject, metadataPath);
    DRB.Logic.AddColumn("IsValidForFilter", domObject, metadataPath + "_filterFields");
}

/**
 * Retrieve Multiple - Add Filter Columns
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.AddFilterColumns = function (domObject, metadataPath) {
    DRB.Logic.RetrieveMultiple.HidePreviousAddButton(metadataPath);
    var container = DRB.DOM.FilterBy.MainDiv.Id;
    if (metadataPath !== "filterCriteria") { container = DRB.DOM.FilterGroups.MainDiv.Id + metadataPath; } // host of a Filter Columns is the base or a group

    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    // navigate to the deepest path, refConfiguration goes by the index and not by path when path is a number
    var splittedMetadataPath = metadataPath.split("_");
    splittedMetadataPath.forEach(function (path) {
        if (isNaN(parseInt(path))) {
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // this is the correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    // FilterColumns are always leaves
    refMetadata.filterType = "fields";
    refConfiguration.filterType = "fields";
    if (!refMetadata.hasOwnProperty("filterFieldsLogic")) { refMetadata.filterFieldsLogic = "and"; }
    if (!refConfiguration.hasOwnProperty("filterFieldsLogic")) { refConfiguration.filterFieldsLogic = "and"; }
    if (!refMetadata.hasOwnProperty("filterFields")) { refMetadata.filterFields = []; }
    if (!refConfiguration.hasOwnProperty("filterFields")) { refConfiguration.filterFields = []; }

    var columnsCriteria = "IsValidForFilter";
    metadataPath += "_filterFields";

    DRB.CustomUI.AddTypeColumns(container, columnsCriteria, domObject, metadataPath);

    // add close button for the field container
    $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).prepend(DRB.UI.CreateCloseButton(DRB.Logic.RetrieveMultiple.RemoveFilterColumns, domObject, metadataPath));

    // add logic dropdown
    $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).append(DRB.UI.CreateEmptyDiv(DRB.DOM[domObject].DivLogic.Id + metadataPath, DRB.DOM[domObject].DivLogic.Class));
    $("#" + DRB.DOM[domObject].DivLogic.Id + metadataPath).append(DRB.UI.CreateSpan(DRB.DOM[domObject].SpanLogic.Id + metadataPath, DRB.DOM[domObject].SpanLogic.Name));
    $("#" + DRB.DOM[domObject].DivLogic.Id + metadataPath).append(DRB.UI.CreateSimpleDropdown(DRB.DOM[domObject].DropdownLogic.Id + metadataPath));
    DRB.UI.FillDropdown(DRB.DOM[domObject].DropdownLogic.Id + metadataPath, DRB.DOM[domObject].DropdownLogic.Name, new DRB.Models.Records(DRB.Settings.OptionsAndOr).ToDropdown());
    DRB.Logic.RetrieveMultiple.BindColumnsLogic(DRB.DOM[domObject].DropdownLogic.Id + metadataPath);
    $("#" + DRB.DOM[domObject].DropdownLogic.Id + metadataPath).val(refMetadata.filterFieldsLogic).change();
    $("#" + DRB.DOM[domObject].DivLogic.Id + metadataPath).hide(); // hide filter logic by default

    // verify if the fields exist
    var fields = JSON.parse(JSON.stringify(refMetadata.filterFields));
    var clearedFields = [];
    fields.forEach(function (field) {
        var currentColumns = DRB.Metadata.CurrentColumns;
        if (DRB.Utilities.HasValue(field.relationship)) {
            var table = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, field.relationship.targetEntity);
            if (DRB.Utilities.HasValue(table)) { currentColumns = table.Columns; }
        }
        var checkColumn = DRB.Utilities.GetRecordById(currentColumns, field.logicalName);
        if (DRB.Utilities.HasValue(checkColumn)) { clearedFields.push(field); }
    });

    // reset Metadata and configuration arrays
    refMetadata.filterFields = [];
    refConfiguration.filterFields = [];

    clearedFields.forEach(function (field, fieldIndex) {
        var fromRelationship = false;
        if (DRB.Utilities.HasValue(field.relationship)) { fromRelationship = true; }
        DRB.Logic.AddColumn(columnsCriteria, domObject, metadataPath, fromRelationship);

        if (fromRelationship === true) {
            $("#" + DRB.DOM[domObject].LookupRelationshipDropdown.Id + metadataPath + "_" + fieldIndex).val(field.relationship.schemaName).change();
        }

        $("#" + DRB.DOM[domObject].Dropdown.Id + metadataPath + "_" + fieldIndex).val(field.logicalName).change();
        // set operator
        $("#" + DRB.DOM[domObject].ControlOperator.Id + metadataPath + "_" + fieldIndex).val(field.operator).change();
        // set value
        var controlPrefix = "";
        var controlPrefixLookup = "cbx2_";
        switch (field.type) {
            case "Uniqueidentifier":
            case "EntityName":
            case "String":
            case "Memo":
            case "Integer":
            case "BigInt":
            case "Decimal":
            case "Double":
            case "Money":
                controlPrefix = "txt_";
                break;

            case "ManagedProperty":
            case "Boolean":
            case "Picklist":
            case "State":
            case "Status":
                controlPrefix = "cbx1_";
                break;

            case "MultiPicklist":
                controlPrefix = "cbxm_";
                break;

            case "Lookup":
            case "Owner":
            case "Customer":
                controlPrefix = "txt2_";
                break;

            case "DateTime":
                controlPrefix = "txtd_";
                break;
        }

        switch (field.type) {
            case "Lookup":
            case "Owner":
            case "Customer":
                if (DRB.Utilities.HasValue(field.value)) {
                    $("#" + controlPrefix + DRB.DOM[domObject].ControlValue.Id + metadataPath + "_" + fieldIndex).val(field.value.id).change();
                    $("#" + controlPrefixLookup + DRB.DOM[domObject].ControlValue.Id + metadataPath + "_" + fieldIndex).val(field.value.entityType).change();
                }
                break;
            default:
                $("#" + controlPrefix + DRB.DOM[domObject].ControlValue.Id + metadataPath + "_" + fieldIndex).val(field.value).change();
                break;
        }
        if (DRB.Utilities.HasValue(field.value2)) {
            $("#" + "second_" + controlPrefix + DRB.DOM[domObject].ControlValue.Id + metadataPath + "_" + fieldIndex).val(field.value2).change();
        }
    });
    $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).show();
}

/**
 * Retrieve Multiple - Remove Filter Groups
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.RemoveFilterGroups = function (domObject, metadataPath) {
    // now remove from the metadata
    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    var splittedMetadataPath = metadataPath.split("_");
    if (splittedMetadataPath.length > 0) { splittedMetadataPath.pop(); }

    splittedMetadataPath.forEach(function (path) {
        if (isNaN(parseInt(path))) {
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // this is the correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    var metadataKeys = Object.keys(refMetadata);
    metadataKeys.forEach(function (key) { if (key !== "Id") { delete refMetadata[key]; } });
    var configurationKeys = Object.keys(refConfiguration);
    configurationKeys.forEach(function (key) { delete refConfiguration[key]; });

    // remove from UI
    $("#" + DRB.DOM[domObject].DivGroups.Id + metadataPath).remove();
    $("#" + DRB.DOM[domObject].DivOptions.Id + metadataPath).remove();
    // show previous Add button
    DRB.Logic.RetrieveMultiple.ShowPreviousAddButton(metadataPath);
}

/**
 * Retrieve Multiple - Remove Filter Columns
 * @param {string} domObject DOM Object
 * @param {string} metadataPath Metadata Path
*/
DRB.Logic.RetrieveMultiple.RemoveFilterColumns = function (domObject, metadataPath) {
    // when removing a filterFields, metadataPath looks like:
    // filterCriteria_filterGroups_2_filterGroups_0_filterFields
    // filterCriteria_filterGroups_1_filterFields
    // filterCriteria_filterFields
    // indexes refer to the metadata object (because they are added based on the UI, not from the data.configuration array)
    // so can happen "filterGroups_5_filterFields" because other groups have been previously removed, the filterGroups array contains just a single item
    // 5 is stored inside the Id property

    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    var splittedMetadataPath = metadataPath.split("_");
    if (splittedMetadataPath.length > 0) { splittedMetadataPath.pop(); } // remove "filterFields" (is always the last one)

    if (splittedMetadataPath.length > 1) {
        var metadataIndexToRemove = parseInt(splittedMetadataPath.pop()); // store metadataIndex To Remove
        // navigate to the deepest path, refConfiguration goes by the index and not by path when path is a number
        splittedMetadataPath.forEach(function (path) {
            if (isNaN(parseInt(path))) {
                if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
                if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
            } else {
                // is a position number
                var metadataIndex = parseInt(path);
                refMetadata.forEach(function (refItem, refItemIndex) {
                    if (refItem.Id === metadataIndex) {
                        // this is the correct path to follow
                        refMetadata = refMetadata[refItemIndex];
                        refConfiguration = refConfiguration[refItemIndex];
                    }
                });
            }
        });

        // remove the item from metadata and configuration
        for (var i = 0; i < refMetadata.length; i++) {
            if (refMetadata[i].Id === metadataIndexToRemove) {
                refMetadata[i] = { Id: metadataIndexToRemove };
                refConfiguration[i] = {};
                break;
            }
        }
    } else {
        // shorter scenario, no groups to verify, the filterFields is attached to the root
        DRB.Metadata.filterCriteria = {};
        DRB.Metadata.CurrentNode.data.configuration.filterCriteria = {};
    }
    // remove from UI
    $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).remove();
    // show previous Add button
    DRB.Logic.RetrieveMultiple.ShowPreviousAddButton(metadataPath);
}

DRB.Logic.RetrieveMultiple.ParseFilterCriteria = function (filterCriteria, metadataPath) {
    // recursive function to parse configuration.filterCriteria into Metadata.filterCriteria

    // filterType must be "fields" or "groups", otherwise return
    if (!filterCriteria.hasOwnProperty("filterType")) { return; }
    if (filterCriteria.filterType !== "fields" && filterCriteria.filterType !== "groups") { return; }

    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;

    var splittedMetadataPath = metadataPath.split("_");

    splittedMetadataPath.forEach(function (path) {
        if (isNaN(parseInt(path))) {
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // this is the correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    // filterFields
    if (filterCriteria.filterType === "fields") {
        refMetadata.filterFields = filterCriteria.filterFields;
        refConfiguration.filterFields = filterCriteria.filterFields;
        DRB.Logic.RetrieveMultiple.AddFilterColumns("FilterColumns", metadataPath);
        // after the "AddFilterColumns" is completed we assign the original filterFieldsLogic and trigger it
        refMetadata.filterFieldsLogic = filterCriteria.filterFieldsLogic;
        refConfiguration.filterFieldsLogic = filterCriteria.filterFieldsLogic;
        $("#" + DRB.DOM.FilterColumns.DropdownLogic.Id + metadataPath + "_filterFields").val(refMetadata.filterFieldsLogic).change();
        return;
    }

    // filterGroups
    if (filterCriteria.filterType === "groups") {
        DRB.Logic.RetrieveMultiple.AddFilterGroups("FilterGroups", metadataPath);
        filterCriteria.filterGroups.forEach(function (filterGroup, filterGroupIndex) {
            DRB.Logic.RetrieveMultiple.AddFilterGroup(DRB.DOM.FilterGroups.DivGroups.Id + metadataPath + "_filterGroups", "FilterGroups", metadataPath + "_filterGroups");
            // hollaback
            DRB.Logic.RetrieveMultiple.ParseFilterCriteria(filterGroup, metadataPath + "_filterGroups" + "_" + filterGroupIndex);
        });

        // after the "AddFilterGroups"/"AddFilterGroup" is completed we assign the original filterFieldsLogic and trigger it
        refMetadata.filterGroupsLogic = filterCriteria.filterGroupsLogic;
        refConfiguration.filterGroupsLogic = filterCriteria.filterGroupsLogic;
        $("#" + DRB.DOM.FilterGroups.DropdownLogic.Id + metadataPath + "_filterGroups").val(refMetadata.filterGroupsLogic).change();
        return;
    }
}

/**
 * Retrieve Multiple - Configure Filter By
*/
DRB.Logic.RetrieveMultiple.ConfigureFilterBy = function () {
    // show main FilterBy div and the "Add" choice button
    $("#" + DRB.DOM.FilterBy.MainDiv.Id).empty();
    $("#" + DRB.DOM.FilterBy.MainDiv.Id).append(DRB.UI.CreateSpan(DRB.DOM.FilterBy.MainSpan.Id, DRB.DOM.FilterBy.MainSpan.Name));
    $("#" + DRB.DOM.FilterBy.MainDiv.Id).append(DRB.UI.CreateSpacer());
    $("#" + DRB.DOM.FilterBy.MainDiv.Id).append(DRB.UI.CreateEmptyDiv(DRB.DOM.FilterGroups.DivChoice.Id + "filterCriteria"));
    $("#" + DRB.DOM.FilterBy.MainDiv.Id).show();

    var metadataPath = "filterCriteria";
    $("#" + DRB.DOM.FilterGroups.DivChoice.Id + metadataPath).append(DRB.UI.CreateButton(DRB.DOM.FilterBy.StartButton.Id, DRB.DOM.FilterBy.StartButton.Name, DRB.DOM.FilterBy.StartButton.Class, DRB.Logic.RetrieveMultiple.StartAddFilter, "FilterGroups", "FilterColumns", metadataPath));

    DRB.Metadata.CurrentNode.data.configuration = DRB.Metadata.CurrentNode.data.configuration || {};
    var configuration = DRB.Metadata.CurrentNode.data.configuration;
    if (!DRB.Utilities.HasValue(configuration.filterCriteria)) { configuration.filterCriteria = {}; }
    DRB.Logic.RetrieveMultiple.TryHydrateCapturedFilterCriteria();

    var filterCriteriaSource = DRB.Utilities.HasValue(configuration.filterCriteria) ? configuration.filterCriteria : {};
    var filterCriteria = {};
    try {
        filterCriteria = JSON.parse(JSON.stringify(filterCriteriaSource));
    } catch {
        filterCriteria = {};
    }

    DRB.Metadata.filterCriteria = {};
    configuration.filterCriteria = {};
    DRB.Logic.RetrieveMultiple.ParseFilterCriteria(filterCriteria, metadataPath);
    DRB.Logic.RetrieveMultiple.RenderCapturedFilterNotice();
}

DRB.Logic.RetrieveMultiple.RenderCapturedFilterNotice = function () {
    var filterContainer = $("#" + DRB.DOM.FilterBy.MainDiv.Id);
    if (filterContainer.length === 0) { return; }
    $("#" + DRB.DOM.FilterBy.CapturedFilterDiv.Id).remove();

    if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || !DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) { return; }
    var configuration = DRB.Metadata.CurrentNode.data.configuration || {};
    var overrides = configuration.capturedQueryOverrides || {};
    if (!DRB.Utilities.HasValue(overrides.filter)) { return; }

    if (DRB.Utilities.HasValue(configuration.filterCriteria) && configuration.filterCriteria.filterType === "fields") { return; }
    if (DRB.Utilities.HasValue(configuration.filterCriteria) && configuration.filterCriteria.filterType === "groups") { return; }

    var capturedDiv = DRB.UI.CreateEmptyDiv(DRB.DOM.FilterBy.CapturedFilterDiv.Id, DRB.DOM.FilterBy.CapturedFilterDiv.Class);
    capturedDiv.append(DRB.UI.CreateSpan(DRB.DOM.FilterBy.CapturedFilterSpan.Id, DRB.DOM.FilterBy.CapturedFilterSpan.Name));
    capturedDiv.append(DRB.UI.CreateSpacer());
    capturedDiv.append(DRB.UI.CreateSpan(DRB.DOM.FilterBy.CapturedFilterHint.Id, DRB.DOM.FilterBy.CapturedFilterHint.Name, null, "text-muted small"));
    capturedDiv.append(DRB.UI.CreateSpacer());
    var textarea = DRB.UI.CreateTextArea(DRB.DOM.FilterBy.CapturedFilterText.Id, DRB.DOM.FilterBy.CapturedFilterText.Class);
    textarea.attr("rows", 4);
    textarea.prop("readonly", true);
    textarea.val(overrides.filter);
    capturedDiv.append(textarea);
    capturedDiv.append(DRB.UI.CreateSpacer());

    var capturedActions = DRB.UI.CreateEmptyDiv(DRB.DOM.FilterBy.CapturedFilterDiv.Id + "_actions", "captured-filter-actions");
    capturedActions.append(DRB.UI.CreateButton(DRB.DOM.FilterBy.CapturedFilterCopyButton.Id, DRB.DOM.FilterBy.CapturedFilterCopyButton.Name, DRB.DOM.FilterBy.CapturedFilterCopyButton.Class, DRB.Logic.RetrieveMultiple.CopyCapturedFilterToClipboard));
    capturedActions.append(DRB.UI.CreateButton(DRB.DOM.FilterBy.CapturedFilterRemoveButton.Id, DRB.DOM.FilterBy.CapturedFilterRemoveButton.Name, DRB.DOM.FilterBy.CapturedFilterRemoveButton.Class, DRB.Logic.RetrieveMultiple.ClearCapturedFilterOverride));
    capturedDiv.append(capturedActions);

    filterContainer.prepend(capturedDiv);
};

DRB.Logic.RetrieveMultiple.CopyCapturedFilterToClipboard = function () {
    var textarea = $("#" + DRB.DOM.FilterBy.CapturedFilterText.Id);
    if (textarea.length === 0) { return; }
    var filterValue = textarea.val();
    if (!DRB.Utilities.HasValue(filterValue)) { return; }
    DRB.Logic.CopyCodeToClipboard(filterValue);
    DRB.UI.ShowMessage("Captured $filter copied to Clipboard");
    setTimeout(function () { DRB.UI.HideLoading(); }, DRB.Settings.TimeoutDelay);
};

DRB.Logic.RetrieveMultiple.ClearCapturedFilterOverride = function () {
    if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || !DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) { return; }
    var configuration = DRB.Metadata.CurrentNode.data.configuration || {};
    if (!DRB.Utilities.HasValue(configuration.capturedQueryOverrides)) { configuration.capturedQueryOverrides = {}; }
    delete configuration.capturedQueryOverrides.filter;
    $("#" + DRB.DOM.FilterBy.CapturedFilterDiv.Id).remove();
};

DRB.Logic.RetrieveMultiple.TryHydrateCapturedFilterCriteria = function () {
    if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || !DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) { return false; }
    var configuration = DRB.Metadata.CurrentNode.data.configuration || {};
    if (!DRB.Utilities.HasValue(configuration.capturedQueryOverrides)) { return false; }
    if (DRB.Utilities.HasValue(configuration.filterCriteria) && DRB.Utilities.HasValue(configuration.filterCriteria.filterType)) { return false; }
    var overrideFilter = configuration.capturedQueryOverrides.filter;
    if (!DRB.Utilities.HasValue(overrideFilter)) { return false; }
    if (!Array.isArray(DRB.Metadata.CurrentColumns) || DRB.Metadata.CurrentColumns.length === 0) { return false; }
    var parsedCriteria = DRB.Logic.RetrieveMultiple.ParseCapturedFilterToCriteria(overrideFilter);
    if (!DRB.Utilities.HasValue(parsedCriteria)) { return false; }
    configuration.filterCriteria = parsedCriteria;
    delete configuration.capturedQueryOverrides.filter;
    return true;
};

DRB.Logic.RetrieveMultiple.ParseCapturedFilterToCriteria = function (filterText) {
    if (!DRB.Utilities.HasValue(filterText)) { return null; }
    var working = filterText.trim();
    if (working.length === 0) { return null; }
    if (working.indexOf('$filter=') === 0) { working = working.substring(8).trim(); }
    var ast = DRB.Logic.RetrieveMultiple.ParseCapturedFilterExpression(working);
    if (!DRB.Utilities.HasValue(ast)) { return null; }
    try {
        return DRB.Logic.RetrieveMultiple.ConvertCapturedAstToCriteria(ast);
    } catch (parseError) {
        console.warn('DRB captured filter parser failed', parseError);
        return null;
    }
};

DRB.Logic.RetrieveMultiple.ParseCapturedFilterExpression = function (expression) {
    if (!DRB.Utilities.HasValue(expression)) { return null; }
    var trimmed = DRB.Logic.RetrieveMultiple.TrimCapturedParentheses(expression.trim());
    if (trimmed.length === 0) { return null; }
    var splitResult = DRB.Logic.RetrieveMultiple.SplitCapturedFilterExpression(trimmed);
    if (!splitResult || splitResult.segments.length === 0) { return null; }
    if (splitResult.segments.length === 1) {
        var condition = DRB.Logic.RetrieveMultiple.ParseCapturedFilterCondition(splitResult.segments[0]);
        if (!condition) { return null; }
        return { type: "condition", condition: condition };
    }
    var logic = splitResult.operators[0] || "and";
    var inconsistentOperator = splitResult.operators.some(function (op) { return op !== logic; });
    if (inconsistentOperator === true) { return null; }
    var children = [];
    var parseFailed = false;
    splitResult.segments.forEach(function (segment) {
        if (parseFailed === true || !DRB.Utilities.HasValue(segment)) { return; }
        var childAst = DRB.Logic.RetrieveMultiple.ParseCapturedFilterExpression(segment);
        if (!childAst) { parseFailed = true; return; }
        children.push(childAst);
    });
    if (parseFailed === true || children.length === 0) { return null; }
    if (children.length === 1) { return children[0]; }
    return { type: "group", logic: logic, children: children };
};

DRB.Logic.RetrieveMultiple.SplitCapturedFilterExpression = function (expression) {
    var result = { segments: [], operators: [] };
    if (!DRB.Utilities.HasValue(expression)) { return result; }
    var depth = 0;
    var inQuotes = false;
    var buffer = '';
    for (var i = 0; i < expression.length; i++) {
        var char = expression[i];
        if (char === "'") {
            if (inQuotes === true && i + 1 < expression.length && expression[i + 1] === "'") {
                buffer += "''";
                i++;
                continue;
            }
            inQuotes = !inQuotes;
            buffer += char;
            continue;
        }
        if (inQuotes === false) {
            if (char === '(') { depth++; buffer += char; continue; }
            if (char === ')') { depth = Math.max(0, depth - 1); buffer += char; continue; }
            if (depth === 0) {
                var remaining = expression.substring(i);
                var remainingLower = remaining.toLowerCase();
                if (remainingLower.indexOf(' and ') === 0) {
                    if (buffer.trim().length > 0) { result.segments.push(buffer.trim()); }
                    result.operators.push('and');
                    buffer = '';
                    i += 4;
                    continue;
                }
                if (remainingLower.indexOf(' or ') === 0) {
                    if (buffer.trim().length > 0) { result.segments.push(buffer.trim()); }
                    result.operators.push('or');
                    buffer = '';
                    i += 3;
                    continue;
                }
            }
        }
        buffer += char;
    }
    if (buffer.trim().length > 0) { result.segments.push(buffer.trim()); }
    return result;
};

DRB.Logic.RetrieveMultiple.TrimCapturedParentheses = function (expression) {
    var working = expression;
    var changed = true;
    while (changed === true) {
        changed = false;
        if (working.startsWith('(') && working.endsWith(')')) {
            var depth = 0;
            var inQuotes = false;
            var removable = true;
            for (var i = 0; i < working.length; i++) {
                var char = working[i];
                if (char === "'") {
                    if (inQuotes === true && i + 1 < working.length && working[i + 1] === "'") { i++; continue; }
                    inQuotes = !inQuotes;
                    continue;
                }
                if (inQuotes === true) { continue; }
                if (char === '(') { depth++; }
                if (char === ')') {
                    depth--;
                    if (depth === 0 && i < working.length - 1) { removable = false; break; }
                }
            }
            if (removable === true && depth === 0) {
                working = working.substring(1, working.length - 1).trim();
                changed = true;
            }
        }
    }
    return working;
};

DRB.Logic.RetrieveMultiple.ParseCapturedFilterCondition = function (segment) {
    if (!DRB.Utilities.HasValue(segment)) { return null; }
    var cleaned = DRB.Logic.RetrieveMultiple.TrimCapturedParentheses(segment.trim());
    if (cleaned.length === 0) { return null; }
    var dynamicsCondition = DRB.Logic.RetrieveMultiple.ParseCapturedMicrosoftFunction(cleaned);
    if (dynamicsCondition) { return dynamicsCondition; }
    var simpleFunction = DRB.Logic.RetrieveMultiple.ParseCapturedSimpleFunction(cleaned);
    if (simpleFunction) { return simpleFunction; }
    return DRB.Logic.RetrieveMultiple.ParseCapturedBinaryCondition(cleaned);
};

DRB.Logic.RetrieveMultiple.ParseCapturedMicrosoftFunction = function (segment) {
    var match = segment.match(/^Microsoft\\.Dynamics\\.CRM\\.([^\\(]+)\\((.*)\\)$/i);
    if (!match) { return null; }
    var operator = match[1].trim();
    var argsText = match[2];
    var args = DRB.Logic.RetrieveMultiple.ParseCapturedFunctionArguments(argsText);
    if (!DRB.Utilities.HasValue(args.PropertyName)) { return null; }
    var fieldPath = DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(args.PropertyName, true);
    var condition = { fieldPath: fieldPath, operator: operator, requiredValue: false };
    if (DRB.Utilities.HasValue(args.PropertyValues)) {
        var parsedArray = DRB.Logic.RetrieveMultiple.ParseCapturedArrayLiteral(args.PropertyValues);
        if (operator === 'Between' || operator === 'NotBetween') {
            if (parsedArray.length > 0) { condition.value = parsedArray[0]; }
            if (parsedArray.length > 1) { condition.value2 = parsedArray[1]; }
            condition.requiredValue = true;
        } else {
            condition.value = parsedArray;
            condition.requiredValue = true;
        }
    }
    if (DRB.Utilities.HasValue(args.PropertyValue)) {
        condition.value = DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(args.PropertyValue);
        condition.requiredValue = true;
    }
    if (DRB.Utilities.HasValue(args.PropertyValue1)) {
        condition.value = DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(args.PropertyValue1);
        condition.requiredValue = true;
    }
    if (DRB.Utilities.HasValue(args.PropertyValue2)) {
        condition.value2 = DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(args.PropertyValue2);
        condition.requiredValue = true;
    }
    return condition;
};

DRB.Logic.RetrieveMultiple.ParseCapturedFunctionArguments = function (argsText) {
    var result = {};
    if (!DRB.Utilities.HasValue(argsText)) { return result; }
    var buffer = '';
    var depth = 0;
    var inQuotes = false;
    var entries = [];
    for (var i = 0; i < argsText.length; i++) {
        var char = argsText[i];
        if (char === "'") {
            if (inQuotes === true && i + 1 < argsText.length && argsText[i + 1] === "'") { buffer += "''"; i++; continue; }
            inQuotes = !inQuotes;
            buffer += char;
            continue;
        }
        if (inQuotes === false) {
            if (char === '[') { depth++; buffer += char; continue; }
            if (char === ']') { depth = Math.max(0, depth - 1); buffer += char; continue; }
            if (char === ',' && depth === 0) {
                if (buffer.trim().length > 0) { entries.push(buffer.trim()); }
                buffer = '';
                continue;
            }
        }
        buffer += char;
    }
    if (buffer.trim().length > 0) { entries.push(buffer.trim()); }
    entries.forEach(function (entry) {
        var splitterIndex = entry.indexOf('=');
        if (splitterIndex === -1) { return; }
        var key = entry.substring(0, splitterIndex).trim();
        var value = entry.substring(splitterIndex + 1).trim();
        result[key] = value;
    });
    return result;
};

DRB.Logic.RetrieveMultiple.ParseCapturedArrayLiteral = function (valueText) {
    var items = [];
    if (!DRB.Utilities.HasValue(valueText)) { return items; }
    var trimmed = valueText.trim();
    if (!trimmed.startsWith('[') || !trimmed.endsWith(']')) { return items; }
    var inner = trimmed.substring(1, trimmed.length - 1);
    var buffer = '';
    var inQuotes = false;
    for (var i = 0; i < inner.length; i++) {
        var char = inner[i];
        if (char === "'") {
            if (inQuotes === true && i + 1 < inner.length && inner[i + 1] === "'") { buffer += "''"; i++; continue; }
            inQuotes = !inQuotes;
            buffer += char;
            continue;
        }
        if (char === ',' && inQuotes === false) {
            if (buffer.trim().length > 0) { items.push(DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(buffer.trim())); }
            buffer = '';
            continue;
        }
        buffer += char;
    }
    if (buffer.trim().length > 0) { items.push(DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(buffer.trim())); }
    return items;
};

DRB.Logic.RetrieveMultiple.SplitCapturedSimpleArguments = function (argsText) {
    var args = [];
    if (!DRB.Utilities.HasValue(argsText)) { return args; }
    var buffer = '';
    var depth = 0;
    var inQuotes = false;
    for (var i = 0; i < argsText.length; i++) {
        var char = argsText[i];
        if (char === "'") {
            if (inQuotes === true && i + 1 < argsText.length && argsText[i + 1] === "'") { buffer += "''"; i++; continue; }
            inQuotes = !inQuotes;
            buffer += char;
            continue;
        }
        if (inQuotes === false) {
            if (char === '(') { depth++; buffer += char; continue; }
            if (char === ')') { depth = Math.max(0, depth - 1); buffer += char; continue; }
            if (char === ',' && depth === 0) {
                if (buffer.trim().length > 0) { args.push(buffer.trim()); }
                buffer = '';
                continue;
            }
        }
        buffer += char;
    }
    if (buffer.trim().length > 0) { args.push(buffer.trim()); }
    return args;
};

DRB.Logic.RetrieveMultiple.ParseCapturedSimpleFunction = function (segment) {
    var match = segment.match(/^([^\\(]+)\\((.*)\\)$/);
    if (!match) { return null; }
    var operator = match[1].trim();
    var argsText = match[2];
    var args = DRB.Logic.RetrieveMultiple.SplitCapturedSimpleArguments(argsText);
    if (args.length === 0) { return null; }
    var condition = { fieldPath: args[0].trim(), operator: operator, requiredValue: args.length > 1 };
    if (args.length > 1) { condition.value = DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(args[1]); }
    if (args.length > 2) { condition.value2 = DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(args[2]); }
    return condition;
};

DRB.Logic.RetrieveMultiple.ParseCapturedBinaryCondition = function (segment) {
    var binary = DRB.Logic.RetrieveMultiple.FindCapturedBinaryOperator(segment);
    if (!binary) { return null; }
    var condition = {
        fieldPath: binary.left.trim(),
        operator: binary.operator,
        requiredValue: binary.right.toLowerCase() !== 'null'
    };
    if (condition.requiredValue === true) {
        condition.value = DRB.Logic.RetrieveMultiple.ParseCapturedLiteral(binary.right);
    }
    return condition;
};

DRB.Logic.RetrieveMultiple.FindCapturedBinaryOperator = function (segment) {
    var operators = [' eq ', ' ne ', ' gt ', ' ge ', ' lt ', ' le '];
    var depth = 0;
    var inQuotes = false;
    var lowerSegment = segment.toLowerCase();
    for (var i = 0; i < segment.length; i++) {
        var char = segment[i];
        if (char === "'") {
            if (inQuotes === true && i + 1 < segment.length && segment[i + 1] === "'") { i++; continue; }
            inQuotes = !inQuotes;
            continue;
        }
        if (inQuotes === true) { continue; }
        if (char === '(') { depth++; continue; }
        if (char === ')') { depth = Math.max(0, depth - 1); continue; }
        if (depth !== 0) { continue; }
        for (var opIndex = 0; opIndex < operators.length; opIndex++) {
            var op = operators[opIndex];
            if (lowerSegment.substring(i).indexOf(op) === 0) {
                var left = segment.substring(0, i).trim();
                var right = segment.substring(i + op.length).trim();
                if (left.length === 0 || right.length === 0) { return null; }
                return { left: left, operator: op.trim(), right: right };
            }
        }
    }
    return null;
};

DRB.Logic.RetrieveMultiple.ParseCapturedLiteral = function (valueText, skipDecode) {
    if (!DRB.Utilities.HasValue(valueText)) { return null; }
    var trimmed = valueText.trim();
    if (trimmed.length === 0) { return null; }
    var lowered = trimmed.toLowerCase();
    if (lowered === 'null') { return null; }
    if (lowered === 'true') { return true; }
    if (lowered === 'false') { return false; }
    if (trimmed.startsWith("'") && trimmed.endsWith("'")) {
        var inner = trimmed.substring(1, trimmed.length - 1);
        inner = inner.replace(/''/g, "'");
        if (skipDecode !== true) {
            try { inner = decodeURIComponent(inner); } catch { }
        }
        return inner;
    }
    if (!isNaN(Number(trimmed))) {
        if (trimmed.indexOf('.') > -1) { return parseFloat(trimmed); }
        return parseInt(trimmed, 10);
    }
    return trimmed;
};

DRB.Logic.RetrieveMultiple.ResolveCapturedFieldMetadata = function (fieldPath) {
    if (!DRB.Utilities.HasValue(fieldPath)) { return null; }
    var cleanedPath = fieldPath.trim();
    var relationshipInfo = null;
    var column = null;
    var managedValueSuffix = false;
    var manyToOne = Array.isArray(DRB.Metadata.CurrentManyToOne) ? DRB.Metadata.CurrentManyToOne : [];
    if (cleanedPath.indexOf('/') === -1) {
        if (cleanedPath.toLowerCase().endsWith('/value')) {
            cleanedPath = cleanedPath.substring(0, cleanedPath.length - 6);
            managedValueSuffix = true;
        }
        column = DRB.Logic.RetrieveMultiple.FindCapturedColumn(DRB.Metadata.CurrentColumns, cleanedPath);
    } else {
        var pathParts = cleanedPath.split('/');
        var navigation = pathParts.shift();
        var columnPath = pathParts.join('/');
        if (columnPath.toLowerCase().endsWith('/value')) {
            columnPath = columnPath.substring(0, columnPath.length - 6);
            managedValueSuffix = true;
        }
        var relationship = DRB.Utilities.GetRecordByProperty(manyToOne, 'NavigationProperty', navigation);
        if (!DRB.Utilities.HasValue(relationship)) { return null; }
        var targetTable = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, relationship.TargetTable);
        if (!DRB.Utilities.HasValue(targetTable)) { return null; }
        column = DRB.Logic.RetrieveMultiple.FindCapturedColumn(targetTable.Columns, columnPath);
        if (!DRB.Utilities.HasValue(column)) { return null; }
        relationshipInfo = {
            schemaName: relationship.SchemaName,
            navigationProperty: relationship.NavigationProperty,
            navigationAttribute: relationship.NavigationAttribute,
            targetEntity: relationship.TargetTable,
            targetEntityLabel: relationship.TargetTableName || relationship.TargetTable,
            targetEntityPrimaryIdField: targetTable.PrimaryIdAttribute
        };
    }
    if (!DRB.Utilities.HasValue(column)) { return null; }
    if (managedValueSuffix === true && column.AttributeType !== 'ManagedProperty') { return null; }
    return { column: column, relationship: relationshipInfo };
};

DRB.Logic.RetrieveMultiple.FindCapturedColumn = function (columns, name) {
    if (!Array.isArray(columns)) { return null; }
    var comparer = name.toLowerCase();
    var column = DRB.Utilities.GetRecordByProperty(columns, 'ODataName', name);
    if (DRB.Utilities.HasValue(column)) { return column; }
    for (var i = 0; i < columns.length; i++) {
        var current = columns[i];
        if (!DRB.Utilities.HasValue(current)) { continue; }
        if (DRB.Utilities.HasValue(current.ODataName) && current.ODataName.toLowerCase() === comparer) { return current; }
        if (DRB.Utilities.HasValue(current.LogicalName) && current.LogicalName.toLowerCase() === comparer) { return current; }
    }
    return null;
};

DRB.Logic.RetrieveMultiple.ConvertCapturedAstToCriteria = function (astNode) {
    if (!DRB.Utilities.HasValue(astNode)) { return null; }
    if (astNode.type === 'condition') {
        var singleField = DRB.Logic.RetrieveMultiple.ConvertCapturedConditionToFilterField(astNode.condition);
        if (!singleField) { return null; }
        return { filterType: 'fields', filterFieldsLogic: 'and', filterFields: [singleField] };
    }
    if (astNode.type === 'group') {
        var allConditions = astNode.children.every(function (child) { return child.type === 'condition'; });
        if (allConditions === true) {
            var fields = [];
            for (var i = 0; i < astNode.children.length; i++) {
                var field = DRB.Logic.RetrieveMultiple.ConvertCapturedConditionToFilterField(astNode.children[i].condition);
                if (!field) { return null; }
                fields.push(field);
            }
            return { filterType: 'fields', filterFieldsLogic: astNode.logic || 'and', filterFields: fields };
        }
        var groups = [];
        for (var childIndex = 0; childIndex < astNode.children.length; childIndex++) {
            var criteria = DRB.Logic.RetrieveMultiple.ConvertCapturedAstToCriteria(astNode.children[childIndex]);
            if (!criteria) { return null; }
            groups.push(criteria);
        }
        if (groups.length === 1) { return groups[0]; }
        return { filterType: 'groups', filterGroupsLogic: astNode.logic || 'and', filterGroups: groups };
    }
    return null;
};

DRB.Logic.RetrieveMultiple.ConvertCapturedConditionToFilterField = function (condition) {
    if (!DRB.Utilities.HasValue(condition) || !DRB.Utilities.HasValue(condition.fieldPath)) { return null; }
    var metadata = DRB.Logic.RetrieveMultiple.ResolveCapturedFieldMetadata(condition.fieldPath);
    if (!metadata) { return null; }
    var column = metadata.column;
    var filterField = {
        logicalName: column.LogicalName,
        schemaName: column.SchemaName,
        label: column.Name,
        type: column.AttributeType,
        oDataName: column.ODataName,
        operator: condition.operator,
        requiredValue: condition.requiredValue === true,
        value: null
    };
    if (metadata.relationship) { filterField.relationship = metadata.relationship; }
    if (column.AttributeType === 'DateTime' && DRB.Utilities.HasValue(column.AdditionalProperties) && DRB.Utilities.HasValue(column.AdditionalProperties.DateTimeBehavior)) {
        filterField.dateTimeBehavior = column.AdditionalProperties.DateTimeBehavior;
    }
    if (condition.requiredValue === true && condition.value !== undefined) {
        filterField.value = DRB.Logic.RetrieveMultiple.NormalizeCapturedValueForField(condition.value, column, condition);
    }
    if (condition.requiredValue === true && condition.value2 !== undefined) {
        filterField.value2 = DRB.Logic.RetrieveMultiple.NormalizeCapturedValueForField(condition.value2, column, condition);
    }
    return filterField;
};

DRB.Logic.RetrieveMultiple.NormalizeCapturedValueForField = function (value, column, condition) {
    if (!DRB.Utilities.HasValue(column)) { return value; }
    if (!DRB.Utilities.HasValue(value)) { return value; }
    switch (column.AttributeType) {
        case 'Integer':
        case 'BigInt':
        case 'Double':
        case 'Decimal':
        case 'Money':
            var numeric = Number(value);
            return isNaN(numeric) ? value : numeric;
        case 'Boolean':
            if (typeof value === 'boolean') { return value; }
            return value.toString().toLowerCase() === 'true';
        case 'Picklist':
        case 'State':
        case 'Status':
            return value.toString();
        case 'MultiPicklist':
            return Array.isArray(value) ? value : [value];
        case 'Lookup':
        case 'Owner':
        case 'Customer':
            if (typeof value === 'object') { return value; }
            var lookupValue = { id: value };
            if (DRB.Utilities.HasValue(column.AdditionalProperties) && Array.isArray(column.AdditionalProperties.Targets) && column.AdditionalProperties.Targets.length === 1) {
                lookupValue.entityType = column.AdditionalProperties.Targets[0];
            }
            return lookupValue;
        default:
            return value;
    }
};

/**
 * Retrieve Multiple - Configure Order Columns
*/
DRB.Logic.RetrieveMultiple.ConfigureOrderColumns = function () {
    var columnsCriteria = "IsValidForOrder";
    var domObject = "OrderColumns";
    var metadataPath = "orderFields";

    // get full Metadata and configuration path
    var refMetadata = DRB.Metadata;
    var refConfiguration = DRB.Metadata.CurrentNode.data.configuration;
    metadataPath.split("_").forEach(function (path) {
        if (isNaN(parseInt(path))) {
            if (refMetadata.hasOwnProperty(path)) { refMetadata = refMetadata[path]; }
            if (refConfiguration.hasOwnProperty(path)) { refConfiguration = refConfiguration[path]; }
        } else {
            // is a position number
            var metadataIndex = parseInt(path);
            refMetadata.forEach(function (refItem, refItemIndex) {
                if (refItem.Id === metadataIndex) {
                    // this is the correct path to follow
                    refMetadata = refMetadata[refItemIndex];
                    refConfiguration = refConfiguration[refItemIndex];
                }
            });
        }
    });

    // show the DOM
    $("#" + DRB.DOM[domObject].MainDiv.Id + metadataPath).show();
    $('#' + DRB.DOM[domObject].Table.Id + metadataPath + ' tr').remove();

    // verify if the fields exist
    var fields = JSON.parse(JSON.stringify(refConfiguration));
    var clearedFields = [];
    fields.forEach(function (field) {
        var checkColumn = DRB.Utilities.GetRecordById(DRB.Metadata.CurrentColumns, field.logicalName);
        if (DRB.Utilities.HasValue(checkColumn)) { clearedFields.push(field); }
    });

    // reset Metadata and configuration arrays
    refMetadata.length = 0;
    refConfiguration.length = 0;

    clearedFields.forEach(function (field, fieldIndex) {
        DRB.Logic.AddColumn(columnsCriteria, domObject, metadataPath);
        $("#" + DRB.DOM[domObject].Dropdown.Id + metadataPath + "_" + fieldIndex).val(field.logicalName).change();
        $("#cbx_" + DRB.DOM[domObject].ControlValue.Id + metadataPath + "_" + fieldIndex).val(field.value).change();
    });
}

/**
 * Retrieve Multiple - After Table Loaded
 * @param {DRB.Models.Table} table Table
*/
DRB.Logic.RetrieveMultiple.AfterTableLoaded = function (table) {
    // Fill Current Metadata
    DRB.Logic.FillCurrentMetadata(table);
    // Fill Relationships Columns
    DRB.Logic.FillRelationshipsColumns();
    // Fill Columns
    DRB.Logic.FillColumns();
    // Fill Relationships
    DRB.Logic.FillRelationships();
    // Fill Alternate Keys
    DRB.Logic.FillAlternateKeys();

    // Fill primaryEntity and PrimaryIdField
    DRB.Metadata.CurrentNode.data.configuration.primaryEntity = { logicalName: table.LogicalName, schemaName: table.SchemaName, label: table.Name, entitySetName: table.EntitySetName };
    DRB.Metadata.CurrentNode.data.configuration.primaryIdField = table.PrimaryIdAttribute;

    DRB.Logic.RetrieveMultiple.ConfigureFilterBy();
    DRB.Logic.RetrieveMultiple.ConfigureOrderColumns();
}

/**
 * Retrieve Multiple - Bind Table
 * @param {string} id Id
*/
DRB.Logic.RetrieveMultiple.BindTable = function (id) {
    $("#" + id).on("change", function (e) {
        var tableLogicalName = $(this).val();
        var table = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, tableLogicalName);
        if (DRB.Utilities.HasValue(table)) {
            if (table.ColumnsLoaded === false || table.RelationshipsLoaded === false || table.AlternateKeysLoaded === false) {
                DRB.UI.ShowLoading("Retrieving Table information...<br /><b>This is a long-running operation</b>");
                setTimeout(function () {
                    DRB.Common.RetrieveTablesDetails([tableLogicalName], true, true)
                        .done(function () {
                            DRB.Common.SetTables(arguments, DRB.Metadata.Tables, true, true);
                            var tableLogicalNames = [];
                            table.OneToManyRelationships.forEach(function (relationship) { tableLogicalNames.push(relationship.SourceTable); tableLogicalNames.push(relationship.TargetTable); });
                            table.ManyToOneRelationships.forEach(function (relationship) { tableLogicalNames.push(relationship.SourceTable); tableLogicalNames.push(relationship.TargetTable); });
                            table.ManyToManyRelationships.forEach(function (relationship) { tableLogicalNames.push(relationship.SourceTable); tableLogicalNames.push(relationship.TargetTable); });
                            tableLogicalNames = DRB.Utilities.RemoveDuplicatesFromArray(tableLogicalNames); // remove duplicates

                            var tablesToRetrieve = [];
                            tableLogicalNames.forEach(function (checkTableLogicalName) {
                                var checkTable = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, checkTableLogicalName);
                                if (DRB.Utilities.HasValue(checkTable) && checkTable.ColumnsLoaded === false) { tablesToRetrieve.push(checkTableLogicalName); }
                            });
                            if (tablesToRetrieve.length > 0) {
                                DRB.Common.RetrieveTablesDetails(tablesToRetrieve, false, true)
                                    .done(function () {
                                        DRB.Common.SetTables(arguments, DRB.Metadata.Tables, false, true);
                                        DRB.Logic.RetrieveMultiple.AfterTableLoaded(table);
                                        DRB.UI.HideLoading();
                                    })
                                    .fail(function (xhr) { DRB.UI.ShowError("DRB.Common.RetrieveTablesDetails Error", DRB.Common.GetErrorMessage(xhr)); });
                            } else {
                                DRB.Logic.RetrieveMultiple.AfterTableLoaded(table);
                                DRB.UI.HideLoading();
                            }
                        })
                        .fail(function (xhr) { DRB.UI.ShowError("DRB.Common.RetrieveTablesDetails Error", DRB.Common.GetErrorMessage(xhr)); });
                }, DRB.Settings.TimeoutDelay);
            } else {
                DRB.Logic.RetrieveMultiple.AfterTableLoaded(table);
            }
        }
    });
}

/**
 * Retrieve Multiple - Start 
 */
DRB.Logic.RetrieveMultiple.Start = function () {
    // Metadata
    DRB.Metadata["filterCriteria"] = {};
    DRB.Metadata["orderFields"] = [];

    // create DOM and bindings
    DRB.CustomUI.AddVersion();
    DRB.CustomUI.AddProcess();
    DRB.CustomUI.AddTokenHeader();
    DRB.CustomUI.AddSpacer();
    DRB.CustomUI.AddImpersonate();
    DRB.CustomUI.AddSpacer();
    DRB.CustomUI.AddFormattedValues();
    DRB.CustomUI.AddRetrieveCount();
    DRB.CustomUI.AddTopCount();
    DRB.CustomUI.AddSpacer();

    // #region Table
    $("#" + DRB.DOM.ConfigureContent.Id).append(DRB.UI.CreateSpan(DRB.DOM.Table.Span.Id, DRB.DOM.Table.Span.Name));
    $("#" + DRB.DOM.ConfigureContent.Id).append(DRB.UI.CreateDropdown(DRB.DOM.Table.Dropdown.Id));
    DRB.UI.FillDropdown(DRB.DOM.Table.Dropdown.Id, DRB.DOM.Table.Dropdown.Name, new DRB.Models.Records(DRB.Metadata.Tables).ToDropdown());
    DRB.Logic.RetrieveMultiple.BindTable(DRB.DOM.Table.Dropdown.Id);
    // #endregion

    DRB.CustomUI.AddColumns();
    DRB.CustomUI.AddSpacer();
    DRB.CustomUI.AddRelationships();

    // #region Add Filter By
    // create FilterBy main div and span
    $("#" + DRB.DOM.ConfigureContent.Id).append(DRB.UI.CreateEmptyDiv(DRB.DOM.FilterBy.MainDiv.Id, DRB.DOM.FilterBy.MainDiv.Class));
    $("#" + DRB.DOM.FilterBy.MainDiv.Id).hide(); // hide by default
    // #endregion

    DRB.CustomUI.AddSpacer();
    DRB.CustomUI.AddTypeColumns(DRB.DOM.ConfigureContent.Id, "IsValidForOrder", "OrderColumns", "orderFields");
    DRB.CustomUI.AddSpacer();

    // #region Triggers
    // events triggered after due to DOM connections to other elements

    // Table
    if (DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data.configuration.primaryEntity)) {
        // check if the table exists
        var checkTable = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, DRB.Metadata.CurrentNode.data.configuration.primaryEntity.logicalName);
        if (!DRB.Utilities.HasValue(checkTable)) {
            // if the table doesn't exist reset the relevant values
            DRB.Metadata.CurrentNode.data.configuration.primaryEntity = null;
            DRB.Metadata.CurrentNode.data.configuration.primaryIdField = "";
            DRB.Metadata.CurrentNode.data.configuration.fields = [];
            DRB.Metadata.CurrentNode.data.configuration.oneToMany = [];
            DRB.Metadata.CurrentNode.data.configuration.manyToOne = [];
            DRB.Metadata.CurrentNode.data.configuration.manyToMany = [];
            DRB.Metadata.CurrentNode.data.configuration.filterCriteria = {};
            DRB.Metadata.CurrentNode.data.configuration.orderFields = [];
        } else {
            $("#" + DRB.DOM.Table.Dropdown.Id).val(DRB.Metadata.CurrentNode.data.configuration.primaryEntity.logicalName).change();
        }
    }
    // #endregion
}
// #endregion
