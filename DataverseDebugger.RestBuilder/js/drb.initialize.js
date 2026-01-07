// #region DRB.Initialize
/**
 * Set Default Settings
 */
DRB.SetDefaultSettings = function () {
    // #region Request Types
    var requests = [{ Id: "retrievesingle", Name: "Retrieve Single" },
    { Id: "retrievemultiple", Name: "Retrieve Multiple" },
    { Id: "create", Name: "Create" },
    { Id: "update", Name: "Update" },
    { Id: "delete", Name: "Delete" },
    { Id: "predefinedquery", Name: "Predefined Query" },
    { Id: "executecustomapi", Name: "Execute Custom API" },
    { Id: "executecustomaction", Name: "Execute Custom Action" },
    { Id: "executeaction", Name: "Execute Action" },
    { Id: "executefunction", Name: "Execute Function" }];
    DRB.Settings.RequestTypes = [];
    requests.forEach(function (request) { DRB.Settings.RequestTypes.push(new DRB.Models.IdValue(request.Id, request.Name)); });
    // #endregion

    // #region Versions
    var versions = ["9.0", "9.1", "9.2"];
    var currentVersion = DRB.Xrm.GetVersion();
    DRB.Settings.Versions = [];
    for (var versionCount = 0; versionCount < versions.length; versionCount++) {
        DRB.Settings.Versions.push(new DRB.Models.IdValue("v" + versions[versionCount], versions[versionCount]));
        if (!DRB.Utilities.HasValue(currentVersion) || currentVersion === versions[versionCount]) { break; }
    }
    // #endregion

    // #region General    
    DRB.Settings.OptionsAyncSync = [new DRB.Models.IdValue("yes", "Asynchronous"), new DRB.Models.IdValue("no", "Synchronous")];
    DRB.Settings.OptionsYesNo = [new DRB.Models.IdValue("yes", "Yes"), new DRB.Models.IdValue("no", "No")];
    DRB.Settings.OptionsViews = [new DRB.Models.IdValue("savedquery", "System View"), new DRB.Models.IdValue("userquery", "Personal View"), new DRB.Models.IdValue("fetchxml", "FetchXML")]; // Predefined Query
    DRB.Settings.OptionsPrevent = [new DRB.Models.IdValue("none", "None"), new DRB.Models.IdValue("create", "Prevent Create"), new DRB.Models.IdValue("update", "Prevent Update")]; // Update
    DRB.Settings.OptionsOrder = [new DRB.Models.IdValue("asc", "Ascending"), new DRB.Models.IdValue("desc", "Descending")]; // Retrieve Multiple
    DRB.Settings.OptionsAndOr = [new DRB.Models.IdValue("and", "And"), new DRB.Models.IdValue("or", "Or")]; // Retrieve Multiple
    DRB.Settings.OptionsTrueFalse = [new DRB.Models.IdValue("yes", "True"), new DRB.Models.IdValue("no", "False")]; // Dataverse Execute
    DRB.Settings.OptionsManageFile = [new DRB.Models.IdValue("retrieve", "Retrieve"), new DRB.Models.IdValue("upload", "Upload"), new DRB.Models.IdValue("delete", "Delete")]; // Manage File Data
    DRB.Settings.OptionsImpersonation = [new DRB.Models.IdValue("mscrmcallerid", "SystemUser Id"), new DRB.Models.IdValue("callerobjectid", "AAD Object Id")]; // Impersonation
    // #endregion

    // #region Operators
    var optNeNull = new DRB.Models.IdValue("ne null", "Contains Data");
    var optEqNull = new DRB.Models.IdValue("eq null", "Does Not Contain Data");

    var optEq = new DRB.Models.IdValue("eq", "Equals");
    var optNe = new DRB.Models.IdValue("ne", "Does Not Equal");

    var optContain = new DRB.Models.IdValue("contains", "Contains");
    var optNotContain = new DRB.Models.IdValue("not contains", "Does Not Contain");

    var optBegin = new DRB.Models.IdValue("startswith", "Begins With");
    var optNotBegin = new DRB.Models.IdValue("not startswith", "Does Not Begin With");

    var optEnd = new DRB.Models.IdValue("endswith", "Ends With");
    var optNotEnd = new DRB.Models.IdValue("not endswith", "Does Not End With");

    var optGreater = new DRB.Models.IdValue("gt", "Is Greater Than");
    var optGreaterEqual = new DRB.Models.IdValue("ge", "Is Greater Than or Equal To");
    var optLess = new DRB.Models.IdValue("lt", "Is Less Than");
    var optLessEqual = new DRB.Models.IdValue("le", "Is Less Than or Equal To");

    var optOn = new DRB.Models.IdValue("eq", "On");
    var optNotOn = new DRB.Models.IdValue("ne", "Not On");
    var optAfter = new DRB.Models.IdValue("gt", "After");
    var optOnOrAfter = new DRB.Models.IdValue("ge", "On or After");
    var optBefore = new DRB.Models.IdValue("lt", "Before");
    var optOnOrBefore = new DRB.Models.IdValue("le", "On or Before");

    var optIn = new DRB.Models.IdValue("In", "Equals");
    var optNotIn = new DRB.Models.IdValue("NotIn", "Does Not Equal");
    var optContainValues = new DRB.Models.IdValue("ContainValues", "Contain Values");
    var optNotContainValues = new DRB.Models.IdValue("DoesNotContainValues", "Does Not Contain Values");

    var optEqCurrentUser = new DRB.Models.IdValue("EqualUserId", "Equals Current User");
    var optNeCurrentUser = new DRB.Models.IdValue("NotEqualUserId", "Does Not Equal Current User");
    var optEqCurrentUserHierarchy = new DRB.Models.IdValue("EqualUserOrUserHierarchy", "Equals Current User Or Their Reporting Hierarchy");
    var optEqCurrentUserHierarchyAndTeams = new DRB.Models.IdValue("EqualUserOrUserHierarchyAndTeams", "Equals Current User And Their Teams Or Their Reporting Hierarchy And Their Teams");
    var optEqCurrentUserTeams = new DRB.Models.IdValue("EqualUserTeams", "Equals Current User's Teams");
    var optEqCurrentUserOrTeams = new DRB.Models.IdValue("EqualUserOrUserTeams", "Equals Current User Or User's Teams");
    var optEqCurrentBusinessUnit = new DRB.Models.IdValue("EqualBusinessId", "Equals Current Business Unit");
    var optNeCurrentBusinessUnit = new DRB.Models.IdValue("NotEqualBusinessId", "Does Not Equal Business Unit");

    // Datetime operators (no required value)
    var optYesterday = new DRB.Models.IdValue("Yesterday", "Yesterday");
    var optToday = new DRB.Models.IdValue("Today", "Today");
    var optTomorrow = new DRB.Models.IdValue("Tomorrow", "Tomorrow");
    var optNext7Days = new DRB.Models.IdValue("Next7Days", "Next 7 Days");
    var optLast7Days = new DRB.Models.IdValue("Last7Days", "Last 7 Days");
    var optNextWeek = new DRB.Models.IdValue("NextWeek", "Next Week");
    var optLastWeek = new DRB.Models.IdValue("LastWeek", "Last Week");
    var optThisWeek = new DRB.Models.IdValue("ThisWeek", "This Week");
    var optNextMonth = new DRB.Models.IdValue("NextMonth", "Next Month");
    var optLastMonth = new DRB.Models.IdValue("LastMonth", "Last Month");
    var optThisMonth = new DRB.Models.IdValue("ThisMonth", "This Month");
    var optNextYear = new DRB.Models.IdValue("NextYear", "Next Year");
    var optLastYear = new DRB.Models.IdValue("LastYear", "Last Year");
    var optThisYear = new DRB.Models.IdValue("ThisYear", "This Year");
    var optNextFiscalYear = new DRB.Models.IdValue("NextFiscalYear", "Next Fiscal Year");
    var optLastFiscalYear = new DRB.Models.IdValue("LastFiscalYear", "Last Fiscal Year");
    var optThisFiscalYear = new DRB.Models.IdValue("ThisFiscalYear", "This Fiscal Year");
    var optNextFiscalPeriod = new DRB.Models.IdValue("NextFiscalPeriod", "Next Fiscal Period");
    var optLastFiscalPeriod = new DRB.Models.IdValue("LastFiscalPeriod", "Last Fiscal Period");
    var optThisFiscalPeriod = new DRB.Models.IdValue("ThisFiscalPeriod", "This Fiscal Period");

    // Datetime operators (required value)
    var optOnDate = new DRB.Models.IdValue("On", "On (Date)");
    var optOnOrAfterDate = new DRB.Models.IdValue("OnOrAfter", "On or After (Date)");
    var optOnOrBeforeDate = new DRB.Models.IdValue("OnOrBefore", "On or Before (Date)");

    var optNextXHours = new DRB.Models.IdValue("NextXHours", "Next X Hours");
    var optLastXHours = new DRB.Models.IdValue("LastXHours", "Last X Hours");
    var optNextXDays = new DRB.Models.IdValue("NextXDays", "Next X Days");
    var optLastXDays = new DRB.Models.IdValue("LastXDays", "Last X Days");
    var optNextXWeeks = new DRB.Models.IdValue("NextXWeeks", "Next X Weeks");
    var optLastXWeeks = new DRB.Models.IdValue("LastXWeeks", "Last X Weeks");
    var optNextXMonths = new DRB.Models.IdValue("NextXMonths", "Next X Months");
    var optLastXMonths = new DRB.Models.IdValue("LastXMonths", "Last X Months");
    var optNextXYears = new DRB.Models.IdValue("NextXYears", "Next X Years");
    var optLastXYears = new DRB.Models.IdValue("LastXYears", "Last X Years");
    var optNextXFiscalYears = new DRB.Models.IdValue("NextXFiscalYears", "Next X Fiscal Years");
    var optLastXFiscalYears = new DRB.Models.IdValue("LastXFiscalYears", "Last X Fiscal Years");
    var optInFiscalYear = new DRB.Models.IdValue("InFiscalYear", "In Fiscal Year");
    var optNextXFiscalPeriods = new DRB.Models.IdValue("NextXFiscalPeriods", "Next X Fiscal Periods");
    var optLastXFiscalPeriods = new DRB.Models.IdValue("LastXFiscalPeriods", "Last X Fiscal Periods");
    var optInFiscalPeriod = new DRB.Models.IdValue("InFiscalPeriod", "In Fiscal Period");
    var optInFiscalPeriodAndYear = new DRB.Models.IdValue("InFiscalPeriodAndYear", "In Fiscal Period and Year");
    var optInOrAfterFiscalPeriodAndYear = new DRB.Models.IdValue("InOrAfterFiscalPeriodAndYear", "In or After Fiscal Period and Year");
    var optInOrBeforeFiscalPeriodAndYear = new DRB.Models.IdValue("InOrBeforeFiscalPeriodAndYear", "In or Before Fiscal Period and Year");
    var optOlderThanXMinutes = new DRB.Models.IdValue("OlderThanXMinutes", "Older Than X Minutes");
    var optOlderThanXHours = new DRB.Models.IdValue("OlderThanXHours", "Older Than X Hours");
    var optOlderThanXDays = new DRB.Models.IdValue("OlderThanXDays", "Older Than X Days");
    var optOlderThanXWeeks = new DRB.Models.IdValue("OlderThanXWeeks", "Older Than X Weeks");
    var optOlderThanXMonths = new DRB.Models.IdValue("OlderThanXMonths", "Older Than X Months");
    var optOlderThanXYears = new DRB.Models.IdValue("OlderThanXYears", "Older Than X Years");

    // Hierarchy Primary Key operators
    var optAbove = new DRB.Models.IdValue("Above", "Above");
    var optAboveOrEqual = new DRB.Models.IdValue("AboveOrEqual", "Above Or Equals");
    var optNotUnder = new DRB.Models.IdValue("NotUnder", "Not Under");
    var optUnder = new DRB.Models.IdValue("Under", "Under");
    var optUnderOrEqual = new DRB.Models.IdValue("UnderOrEqual", "Under Or Equals");

    var optEqUserLanguage = new DRB.Models.IdValue("EqualUserLanguage", "Equals User Language"); // operator valid for number and choice
    var optBetween = new DRB.Models.IdValue("Between", "Between");
    var optNotBetween = new DRB.Models.IdValue("NotBetween", "Not Between");

    DRB.Settings.OptionsOperatorBasic = [optEq, optNe, optNeNull, optEqNull];
    DRB.Settings.OptionsOperatorHierarchyPrimaryKey = [optEq, optNe, optNeNull, optEqNull, optAbove, optAboveOrEqual, optNotUnder, optUnder, optUnderOrEqual];
    DRB.Settings.OptionsOperatorLookupBusinessUnit = [optEq, optNe, optNeNull, optEqNull, optEqCurrentBusinessUnit, optNeCurrentBusinessUnit];
    DRB.Settings.OptionsOperatorLookupUser = [optEq, optNe, optNeNull, optEqNull, optEqCurrentUser, optNeCurrentUser];
    DRB.Settings.OptionsOperatorOwner = [optEq, optNe, optNeNull, optEqNull, optEqCurrentUser, optNeCurrentUser, optEqCurrentUserHierarchy, optEqCurrentUserHierarchyAndTeams, optEqCurrentUserTeams, optEqCurrentUserOrTeams];
    DRB.Settings.OptionsOperatorString = [optEq, optNe, optContain, optNotContain, optBegin, optNotBegin, optEnd, optNotEnd, optNeNull, optEqNull, optBetween, optNotBetween];
    DRB.Settings.OptionsOperatorMemo = [optContain, optNotContain, optBegin, optNotBegin, optEnd, optNotEnd, optNeNull, optEqNull];
    DRB.Settings.OptionsOperatorPicklist = [optEq, optNe, optNeNull, optEqNull, , optBetween, optNotBetween, optEqUserLanguage];
    DRB.Settings.OptionsOperatorMultiPicklist = [optIn, optNotIn, optContainValues, optNotContainValues, optNeNull, optEqNull];
    DRB.Settings.OptionsOperatorNumber = [optEq, optNe, optGreater, optGreaterEqual, optLess, optLessEqual, optNeNull, optEqNull, , optBetween, optNotBetween, optEqUserLanguage];
    DRB.Settings.OptionsOperatorDateTime = [optOn, optOnDate, optNotOn, optAfter, optOnOrAfter, optOnOrAfterDate, optBefore, optOnOrBefore, optOnOrBeforeDate, optNeNull, optEqNull,
        optYesterday, optToday, optTomorrow, optNext7Days, optLast7Days, optNextWeek, optLastWeek, optThisWeek, optNextMonth, optLastMonth, optThisMonth, optNextYear, optLastYear, optThisYear, optNextFiscalYear, optLastFiscalYear, optThisFiscalYear, optNextFiscalPeriod, optLastFiscalPeriod, optThisFiscalPeriod,
        optNextXHours, optLastXHours, optNextXDays, optLastXDays, optNextXWeeks, optLastXWeeks, optNextXMonths, optLastXMonths, optNextXYears, optLastXYears, optNextXFiscalYears, optLastXFiscalYears, optInFiscalYear, optNextXFiscalPeriods, optLastXFiscalPeriods, optInFiscalPeriod,
        optInFiscalPeriodAndYear, optInOrAfterFiscalPeriodAndYear, optInOrBeforeFiscalPeriodAndYear, optOlderThanXMinutes, optOlderThanXHours, optOlderThanXDays, optOlderThanXWeeks, optOlderThanXMonths, optOlderThanXYears, optBetween, optNotBetween];

    DRB.Settings.OperatorsToStop = [optNeNull, optEqNull, optEqCurrentUser, optNeCurrentUser, optEqCurrentUserHierarchy, optEqCurrentUserHierarchyAndTeams, optEqCurrentUserTeams, optEqCurrentUserOrTeams, optEqCurrentBusinessUnit, optNeCurrentBusinessUnit,
        optYesterday, optToday, optTomorrow, optNext7Days, optLast7Days, optNextWeek, optLastWeek, optThisWeek, optNextMonth, optLastMonth, optThisMonth, optNextYear, optLastYear, optThisYear, optNextFiscalYear, optLastFiscalYear, optThisFiscalYear, optNextFiscalPeriod, optLastFiscalPeriod, optThisFiscalPeriod, optEqUserLanguage];

    DRB.Settings.OperatorsTwoValues = [optInFiscalPeriodAndYear, optInOrAfterFiscalPeriodAndYear, optInOrBeforeFiscalPeriodAndYear, optBetween, optNotBetween];

    DRB.Settings.OperatorIdsAllowedDepth = [optNeNull.Id, optEqNull.Id, optEq.Id, optNe.Id, optContain.Id, optNotContain.Id, optBegin.Id, optNotBegin.Id, optEnd.Id, optNotEnd.Id, optGreater.Id, optGreaterEqual.Id,
    optLess.Id, optLessEqual.Id, optOn.Id, optNotOn.Id, optAfter.Id, optOnOrAfter.Id, optBefore.Id, optOnOrBefore.Id];
    // #endregion

    DRB.Settings.TimeoutDelay = 500; // used in the setTimout calls
    DRB.Settings.IsInitialized = false;
}

/**
 * Define Operations
 */
DRB.DefineOperations = function () {
    // #region Menu
    var inp_LoadFile = DRB.UI.CreateInputFile(DRB.DOM.Collection.LoadInput.Id, true, DRB.Collection.Parse);
    var btn_LoadCollection = DRB.UI.CreateButton(DRB.DOM.Collection.LoadButton.Id, DRB.DOM.Collection.LoadButton.Name, DRB.DOM.Collection.LoadButton.Class, DRB.Collection.Load);
    var btn_SaveCollection = DRB.UI.CreateButton(DRB.DOM.Collection.SaveButton.Id, DRB.DOM.Collection.SaveButton.Name, DRB.DOM.Collection.SaveButton.Class, DRB.Collection.Save);

    var menu = $("#" + DRB.DOM.Collection.Menu.Id);
    menu.append(inp_LoadFile);
    menu.append(btn_LoadCollection);
    menu.append(btn_SaveCollection);
    // #endregion

    // #region jsTree
    $("#" + DRB.DOM.TreeView.Id).jstree({
        "core": { "data": [], "themes": { "dots": false }, "check_callback": true }, // default settings
        "contextmenu": { // right click menu
            "select_node": false,
            "items": function (node) {
                var customItems = {
                    "createrequest": {
                        "label": "Create Request",
                        "action": function (data) {
                            var inst = $.jstree.reference(data.reference);
                            var obj = inst.get_node(data.reference);
                            var parent = obj;
                            while (parent && parent.type === "request") {
                                parent = inst.get_node(parent.parent);
                            }
                            if (!parent || parent.id === "#") {
                                var roots = inst.get_node("#").children;
                                if (roots.length > 0) {
                                    parent = inst.get_node(roots[0]);
                                }
                            }
                            inst.create_node(parent, { "type": "request", "text": "New Request" }, "last", function (new_node) {
                                try { inst.edit(new_node); } catch (ex) { setTimeout(function () { inst.edit(new_node); }, 0); }
                            });
                        }
                    },
                    "rename": {
                        "label": "Rename",
                        "action": function (data) {
                            var inst = $.jstree.reference(data.reference);
                            var obj = inst.get_node(data.reference);
                            try { inst.edit(obj); } catch (ex) { setTimeout(function () { inst.edit(obj); }, 0); }
                        }
                    },
                    "delete": {
                        "label": "Delete",
                        "action": function (data) {
                            var inst = $.jstree.reference(data.reference);
                            var obj = inst.get_node(data.reference);
                            inst.delete_node(obj);
                        }
                    }
                };
                if (node.type !== "request") { delete customItems["delete"]; }
                return customItems;
            }
        },
        "types": { // node types
            "#": { "valid_children": ["collection"] }, // "root" can have only "collection" nodes
            "collection": { "icon": "hide-icon", "valid_children": ["folder", "request"] }, // "collection" can have only "folder" and "request" nodes, no icon
            "folder": { "valid_children": ["folder", "request"] }, // "folder" can have only "folder" and "request" nodes, default icon
            "request": { "icon": "jstree-file", "valid_children": [] } // "request" can't have nodes, file icon
        },
        "plugins": ["types", "contextmenu"] // node types, right click menu
    });

    $("#" + DRB.DOM.TreeView.Id).on("ready.jstree refresh.jstree", function (e, data) {
        data.instance.open_all();
    });

    $("#" + DRB.DOM.TreeView.Id).on("select_node.jstree", function (e, data) {
        data.instance.toggle_node(data.selected);  // single click to expand
        DRB.Logic.EditRequest(data.node);
    });

    $("#" + DRB.DOM.TreeView.Id).on("rename_node.jstree", function (e, obj) {
        if (DRB.Utilities.HasValue(DRB.Metadata.CurrentNode)) {
            if (DRB.Metadata.CurrentNode.type === "request" && DRB.Metadata.CurrentNode.id === obj.node.id) {
                $("#" + DRB.DOM.RequestType.Div.Id).text(obj.node.text);
            }
        }
    });

    $("#" + DRB.DOM.TreeView.Id).on("delete_node.jstree", function (e, obj) {
        if (DRB.Utilities.HasValue(DRB.Metadata.CurrentNode)) {
            if (DRB.Metadata.CurrentNode.id === obj.node.id || DRB.Metadata.CurrentNode.parents.indexOf(obj.node.id) > -1) {
                DRB.Metadata.CurrentNode = null;
                $("#" + DRB.DOM.MainContent.Id).hide();
            }
        }
    });
    // #endregion

    // #region Request Type
    var container = DRB.UI.CreateWideContainerWithId(DRB.DOM.RequestType.Div.Id, DRB.DOM.RequestType.Div.Name);
    $("#" + DRB.DOM.MainContent.Id).append(container);
    var requestControls = $("<div>", { class: "drb-request-controls" });
    requestControls.append(DRB.UI.CreateSpan(DRB.DOM.RequestType.Span.Id, DRB.DOM.RequestType.Span.Name));
    requestControls.append(DRB.UI.CreateSimpleDropdown(DRB.DOM.RequestType.Dropdown.Id));
    var btn_executeRequest = DRB.UI.CreateButton("btn_execute_request", "&#x25B6; Execute", "btn-danger", function () {
        DRB.GenerateCode.Start();
        try {
            var fetchEditor = DRB.Settings.Editors["code_fetchapi"];
            var executeEditor = DRB.Settings.Editors[DRB.Settings.TabExecute];
            if (DRB.Utilities.HasValue(fetchEditor) && DRB.Utilities.HasValue(executeEditor)) {
                executeEditor.session.setValue(fetchEditor.session.getValue());
            }
        } catch { }
        DRB.Logic.ExecuteCodeFromEditor();
    });
    requestControls.append(btn_executeRequest);
    container.append(requestControls);
    container.append(DRB.UI.CreateSpacer());
    DRB.UI.FillDropdown(DRB.DOM.RequestType.Dropdown.Id, DRB.DOM.RequestType.Dropdown.Name, new DRB.Models.Records(DRB.Settings.RequestTypes).ToDropdown(), false, false, false, 8);
    DRB.Logic.BindRequestType(DRB.DOM.RequestType.Dropdown.Id);
    // #endregion

    // #region Tabs
    DRB.Settings.Tabs = [];
    DRB.Settings.Tabs.push({ Id: "configure", Name: "Configure", ConfigureContent: true });
    DRB.Settings.Tabs.push({ Id: "code_fetchapi", Name: "Fetch", GenerateCode: true, ShowEditor: true, EditorMode: "javascript", CopyCode: true, MoveToEditor: true, ShowWarning: true, WarningClientUrl: true });
    DRB.Settings.Tabs.push({ Id: "code_editor", Name: "Editor", ShowEditor: true, EditorMode: "javascript", CopyCode: true, Execute: true, ShowWarning: true, WarningEditor: true });
    DRB.Settings.Tabs.push({ Id: "code_results", Name: "Results", ShowEditor: true, EditorMode: "json", CopyCode: true, Results: true, ShowWarning: true, WarningResults: true });

    var tabs_Request = DRB.UI.CreateTabs(DRB.DOM.TabsRequest.Id, DRB.Settings.Tabs);
    var tabs_Content = DRB.UI.CreateTabContents(DRB.DOM.TabsContent.Id, DRB.Settings.Tabs);

    $("#" + DRB.DOM.MainContent.Id).append(tabs_Request);
    $("#" + DRB.DOM.MainContent.Id).append(tabs_Content);

    DRB.Settings.Tabs.forEach(function (tab) {
        $("#" + tab.Id).append(DRB.UI.CreateSpacer());

        if (DRB.Utilities.HasValue(tab.ConfigureContent) && tab.ConfigureContent === true) {
            $("#" + tab.Id).append(DRB.UI.CreateEmptyDiv(DRB.DOM.ConfigureContent.Id));
        }

        if (DRB.Utilities.HasValue(tab.CopyCode) && tab.CopyCode === true) {
            if (DRB.Utilities.HasValue(tab.Results) && tab.Results === true) {
                var btn_copyResults = DRB.UI.CreateButton("btn_" + tab.Id + "_copy", "Copy Results", "btn-secondary", DRB.Logic.CopyCodeFromEditor, tab.Id);
                $("#" + tab.Id).append(btn_copyResults);
            } else {
                var btn_copyCode = DRB.UI.CreateButton("btn_" + tab.Id + "_copy", "Copy Code", "btn-secondary", DRB.Logic.CopyCodeFromEditor, tab.Id);
                $("#" + tab.Id).append(btn_copyCode);
            }
        }

        if (DRB.Utilities.HasValue(tab.MoveToEditor) && tab.MoveToEditor === true) {
            var btn_moveCode = DRB.UI.CreateButton("btn_" + tab.Id + "_move", "Move Code to Editor", "btn-secondary", DRB.Logic.MoveCodeToMainEditor, tab.Id);
            $("#" + tab.Id).append(btn_moveCode);
        }

        if (DRB.Utilities.HasValue(tab.Execute) && tab.Execute === true) {
            var btn_executeCode = DRB.UI.CreateButton("btn_" + tab.Id + "_execute", "Execute Code", "btn-danger", DRB.Logic.ExecuteCodeFromEditor);
            $("#" + tab.Id).append(btn_executeCode);
        }

        if (DRB.Utilities.HasValue(tab.RefreshGrid) && tab.RefreshGrid === true) {
            var btn_refreshGrid = DRB.UI.CreateButton("btn_" + tab.Id + "_refreshgrid", "Refresh", "btn-danger", DRB.Logic.RefreshGrid);
            $("#" + tab.Id).append(btn_refreshGrid);
        }

        if (DRB.Utilities.HasValue(tab.ShowWarning) && tab.ShowWarning === true) {
            $("#" + tab.Id).append(DRB.UI.CreateSpan(DRB.DOM.TabsWarning.Id + tab.Id, ""));
        }

        if (DRB.Utilities.HasValue(tab.ShowEditor) && tab.ShowEditor === true) {
            $("#" + tab.Id).append(DRB.UI.CreateSpacer());
            $("#" + tab.Id).append(DRB.UI.CreateEmptyDiv(tab.Id + "_editor", "code_editor"));
        }

        if (DRB.Utilities.HasValue(tab.EmptyDiv) && tab.EmptyDiv === true) {
            $("#" + tab.Id).append(DRB.UI.CreateEmptyDiv(tab.Id + "_div"));
        }
    });
    // #endregion

    // #region Editors
    DRB.Settings.Editors = [];
    DRB.Settings.TabExecute = "";
    DRB.Settings.TabResults = "";

    DRB.Settings.Tabs.forEach(function (tab) {
        if (DRB.Utilities.HasValue(tab.Execute) && tab.Execute === true) { DRB.Settings.TabExecute = tab.Id; }
        if (DRB.Utilities.HasValue(tab.Results) && tab.Results === true) { DRB.Settings.TabResults = tab.Id; }

        if (DRB.Utilities.HasValue(tab.ShowEditor) && tab.ShowEditor === true) {
            DRB.Settings.Editors[tab.Id] = ace.edit(tab.Id + "_editor", { useWorker: false });
            DRB.Settings.Editors[tab.Id].setShowPrintMargin(false);
            if (DRB.Utilities.HasValue(tab.EditorMode)) { DRB.Settings.Editors[tab.Id].session.setMode("ace/mode/" + tab.EditorMode); }
            if (DRB.Utilities.HasValue(tab.GenerateCode) && tab.GenerateCode === true) {
                DRB.Settings.Editors[tab.Id].setOptions({ readOnly: true });
            }
        }
    });
    // #endregion
}

DRB.Theme = DRB.Theme || (function () {
    var current = "dark";
    var listeners = [];
    var domListenerAttached = false;

    function updateDom() {
        if (document.readyState === "loading") {
            if (domListenerAttached === true) { return; }
            domListenerAttached = true;
            var onReady = function () {
                domListenerAttached = false;
                document.removeEventListener("DOMContentLoaded", onReady);
                updateDom();
            };
            document.addEventListener("DOMContentLoaded", onReady);
            return;
        }

        var body = document.body;
        if (!body) { return; }

        body.dataset.drbTheme = current;
        body.classList.remove("drb-theme-dark", "drb-theme-light");
        body.classList.add("drb-theme-" + current);
        if (document.documentElement) {
            document.documentElement.style.setProperty("color-scheme", current);
        }
    }

    function applyTheme(theme) {
        var normalized = theme === "light" ? "light" : "dark";
        if (current === normalized && document.body && document.body.dataset.drbTheme === normalized) { return; }
        current = normalized;
        updateDom();
        listeners.forEach(function (callback) {
            try {
                callback(normalized);
            } catch (e) { }
        });
    }

    function onChange(callback) {
        if (typeof callback === "function") {
            listeners.push(callback);
        }
    }

    updateDom();

    return {
        apply: applyTheme,
        onChange: onChange,
        current: function () { return current; }
    };
}());

if (typeof window !== "undefined" && typeof window.__drbApplyTheme !== "function") {
    window.__drbApplyTheme = function (theme) {
        if (window.DRB && DRB.Theme) {
            DRB.Theme.apply(theme);
        }
    };
}

// Capture injection entrypoint for WebView host
if (typeof window !== "undefined") {
    var __drbPendingCapturedRequests = [];

    window.__drbAddPendingCapturedRequest = function (payload) {
        try {
            if (!DRB.Utilities.HasValue(payload)) { return; }
            __drbPendingCapturedRequests.push(payload);
        } catch (ex) {
            console.error(ex);
        }
    };

    window.__drbFlushPendingCapturedRequests = function () {
        try {
            if (__drbPendingCapturedRequests.length === 0) { return; }
            if (!DRB.Utilities.HasValue(DRB.Settings) || DRB.Settings.IsInitialized !== true) { return; }
            var pending = __drbPendingCapturedRequests.slice();
            __drbPendingCapturedRequests.length = 0;
            pending.forEach(function (pendingPayload) {
                window.__drbReceiveCapturedRequest(pendingPayload);
            });
        } catch (ex) {
            console.error(ex);
        }
    };

    window.__drbReceiveCapturedRequest = function (payload) {
        try {
            if (!DRB.Utilities.HasValue(payload)) { return; }
            if (!DRB.Utilities.HasValue(DRB.Settings) || DRB.Settings.IsInitialized !== true) {
                window.__drbAddPendingCapturedRequest(payload);
                return;
            }
            var treeElement = $("#" + DRB.DOM.TreeView.Id);
            if (treeElement.length === 0) { return; }
            var tree = treeElement.jstree(true);
            if (!DRB.Utilities.HasValue(tree)) { return; }

            var hasCollectionNode = function (node) {
                return DRB.Utilities.HasValue(node) && DRB.Utilities.HasValue(node.children) && node.children.length > 0;
            };

            var root = tree.get_node("#");
            if (!hasCollectionNode(root)) {
                var resumeAfterReady = (function () {
                    var invoked = false;
                    return function () {
                        if (invoked === true) { return; }
                        invoked = true;
                        setTimeout(function () { window.__drbReceiveCapturedRequest(payload); }, 0);
                    };
                })();

                treeElement.one("refresh.jstree", resumeAfterReady);
                treeElement.one("ready.jstree", resumeAfterReady);

                if (treeElement.hasClass("jstree-loading") === true) {
                    return;
                }

                DRB.Collection.CreateDefault();
                return;
            }
            var parentId = root.children[0];
            var requestName = DRB.Utilities.HasValue(payload.requestName) ? payload.requestName : "Captured request";
            var nodeData = { endpoint: "webapi", requestType: payload.requestType || "", configuration: {}, capture: payload };
            var newNodeId = tree.create_node(parentId, { type: "request", text: requestName, data: nodeData }, "last");
            if (!DRB.Utilities.HasValue(newNodeId)) { return; }
            tree.open_node(parentId);
            tree.deselect_all();
            tree.select_node(newNodeId);
            setTimeout(function () { window.__drbPopulateCapturedRequest(newNodeId, payload); }, 400);
        } catch (ex) {
            console.error(ex);
        }
    };

    window.__drbResolveCapturedRequestType = function (payload) {
        try {
            if (!DRB.Utilities.HasValue(payload)) { return ""; }
            if (DRB.Utilities.HasValue(payload.requestType)) { return payload.requestType; }
            return window.__drbInferRequestTypeFromOperation(payload);
        } catch (ex) {
            console.error(ex);
            return "";
        }
    };

    window.__drbInferRequestTypeFromOperation = function (payload) {
        try {
            if (!DRB.Utilities.HasValue(payload) || !DRB.Utilities.HasValue(payload.dataverseOperationName)) { return ""; }
            var method = payload.method || payload.Method || "";
            method = method ? method.toUpperCase() : "";
            var isFunction = method === "GET";
            var isCustom = payload.dataverseOperationName.indexOf("_") > 0;
            if (isFunction) { return isCustom ? "executecustomapi" : "executefunction"; }
            return isCustom ? "executecustomaction" : "executeaction";
        } catch (inferError) {
            console.error(inferError);
            return "";
        }
    };

    window.__drbIsDataverseRequestType = function (requestType) {
        if (!DRB.Utilities.HasValue(requestType)) { return false; }
        var normalized = requestType.toLowerCase();
        return normalized === "executeaction" || normalized === "executefunction" ||
            normalized === "executecustomaction" || normalized === "executecustomapi";
    };

    window.__drbIsDataverseMetadataReady = function (requestType) {
        try {
            if (!window.__drbIsDataverseRequestType(requestType)) { return true; }
            if (typeof DRB === "undefined" || !DRB.Utilities.HasValue(DRB.Metadata)) { return false; }
            var normalized = requestType.toLowerCase();
            switch (normalized) {
                case "executecustomapi":
                    return DRB.Metadata.DataverseCustomAPIsLoaded === true;
                case "executecustomaction":
                    return DRB.Metadata.DataverseCustomActionsLoaded === true;
                case "executeaction":
                case "executefunction":
                    return DRB.Metadata.DataverseMetadataLoaded === true;
                default:
                    return true;
            }
        } catch (metadataError) {
            console.error(metadataError);
            return false;
        }
    };

    window.__drbDeferCapturedDataverseRequest = function (requestType, nodeId, payload) {
        try {
            if (!DRB.Utilities.HasValue(requestType)) { return; }
            if (!window.__drbPendingDataverseMetadata) { window.__drbPendingDataverseMetadata = {}; }
            var normalized = requestType.toLowerCase();
            var queue = window.__drbPendingDataverseMetadata[normalized];
            if (!Array.isArray(queue)) { queue = []; }
            var replaced = false;
            for (var i = 0; i < queue.length; i++) {
                if (queue[i].nodeId === nodeId) {
                    queue[i] = { nodeId: nodeId, payload: payload };
                    replaced = true;
                    break;
                }
            }
            if (replaced !== true) {
                queue.push({ nodeId: nodeId, payload: payload });
            }
            window.__drbPendingDataverseMetadata[normalized] = queue;
        } catch (deferError) {
            console.error(deferError);
        }
    };

    window.__drbOnDataverseMetadataReady = function (requestType) {
        try {
            if (!window.__drbPendingDataverseMetadata) { return; }
            var normalized = DRB.Utilities.HasValue(requestType) ? requestType.toLowerCase() : "";
            var keysToFlush = [];
            if (normalized === "executeaction" || normalized === "executefunction") {
                ["executeaction", "executefunction"].forEach(function (key) {
                    if (window.__drbPendingDataverseMetadata[key]) { keysToFlush.push(key); }
                });
            } else if (normalized.length > 0) {
                if (window.__drbPendingDataverseMetadata[normalized]) { keysToFlush.push(normalized); }
            } else {
                keysToFlush = Object.keys(window.__drbPendingDataverseMetadata);
            }
            keysToFlush.forEach(function (key) {
                var queue = window.__drbPendingDataverseMetadata[key];
                if (!Array.isArray(queue) || queue.length === 0) { return; }
                delete window.__drbPendingDataverseMetadata[key];
                queue.forEach(function (entry) {
                    setTimeout(function () {
                        try {
                            if (typeof window.__drbPopulateCapturedRequest === "function") {
                                window.__drbPopulateCapturedRequest(entry.nodeId, entry.payload);
                            }
                        } catch (replayError) {
                            console.error(replayError);
                        }
                    }, 0);
                });
            });
        } catch (notifyError) {
            console.error(notifyError);
        }
    };

    window.__drbScheduleCapturedDataverseExecuteSelection = function (nodeId, payload, attempt) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
            if (!DRB.Utilities.HasValue(payload.dataverseOperationName)) { return; }
            if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || DRB.Metadata.CurrentNode.id !== nodeId) { return; }

            if (!payload.__drbDataverseSelectionStart) { payload.__drbDataverseSelectionStart = Date.now(); }
            var elapsed = Date.now() - payload.__drbDataverseSelectionStart;
            var maxWait = 120000;
            if (elapsed > maxWait) {
                delete payload.__drbDataverseSelectionStart;
                return;
            }

            var dropdown = $("#" + DRB.DOM.DataverseExecute.Dropdown.Id);
            if (dropdown.length === 0) {
                setTimeout(function () { window.__drbScheduleCapturedDataverseExecuteSelection(nodeId, payload, (attempt || 0) + 1); }, 300);
                return;
            }

            var previousValue = dropdown.val();
            dropdown.val(payload.dataverseOperationName);
            if (dropdown.val() === payload.dataverseOperationName) {
                dropdown.change();
                delete payload.__drbDataverseSelectionStart;
                return;
            }

            dropdown.val(previousValue);
            setTimeout(function () { window.__drbScheduleCapturedDataverseExecuteSelection(nodeId, payload, (attempt || 0) + 1); }, 300);
        } catch (dataverseSelectionError) {
            console.error(dataverseSelectionError);
        }
    };

    window.__drbCacheCapturedDataverseParameters = function (nodeId, payload) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
            if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || DRB.Metadata.CurrentNode.id !== nodeId) { return; }
            var parsedBody = window.__drbParseCapturedRequestBody(payload);
            if (!DRB.Utilities.HasValue(parsedBody) || typeof parsedBody !== "object") { parsedBody = {}; }
            DRB.Metadata.CurrentNode.data.__capturedDataverseParameters = parsedBody;
            var bindingEntitySet = payload.entitySetName;
            if (!DRB.Utilities.HasValue(bindingEntitySet) && DRB.Utilities.HasValue(payload.primaryEntityLogicalName) && Array.isArray(DRB.Metadata.Tables)) {
                var tableRecord = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, payload.primaryEntityLogicalName);
                if (DRB.Utilities.HasValue(tableRecord) && DRB.Utilities.HasValue(tableRecord.EntitySetName)) {
                    bindingEntitySet = tableRecord.EntitySetName;
                }
            }
            DRB.Metadata.CurrentNode.data.__capturedDataverseBinding = {
                logicalName: payload.primaryEntityLogicalName || "",
                primaryId: payload.primaryId || "",
                entitySetName: bindingEntitySet || ""
            };
        } catch (cacheError) {
            console.error(cacheError);
        }
    };

    window.__drbBuildCapturedDataverseParameters = function (dvExecute) {
        try {
            if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || !DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) { return null; }
            var captured = DRB.Metadata.CurrentNode.data.__capturedDataverseParameters;
            if (!DRB.Utilities.HasValue(captured)) { captured = {}; }

            var results = [];
            dvExecute.Parameters.forEach(function (parameter) {
                var rawValue = captured[parameter.Name];
                var includeValue = parameter.Optional !== true;
                var convertedValue = null;
                var hasCapturedValue = Object.prototype.hasOwnProperty.call(captured, parameter.Name);
                if (hasCapturedValue) {
                    includeValue = true;
                    convertedValue = window.__drbConvertCapturedDataverseValue(parameter.Type, rawValue);
                } else {
                    var derivedValue = window.__drbDeriveDataverseBindingValue(parameter);
                    if (DRB.Utilities.HasValue(derivedValue)) {
                        includeValue = true;
                        convertedValue = derivedValue;
                    }
                }
                results.push({
                    name: parameter.Name,
                    type: parameter.Type,
                    optional: parameter.Optional === true,
                    include: includeValue,
                    value: convertedValue
                });
            });

            DRB.Metadata.CurrentNode.data.__capturedDataverseParameters = null;
            DRB.Metadata.CurrentNode.data.__capturedDataverseBinding = null;
            return results;
        } catch (buildError) {
            console.error(buildError);
            return null;
        }
    };

    window.__drbConvertCapturedDataverseValue = function (parameterType, rawValue) {
        try {
            if (!DRB.Utilities.HasValue(parameterType)) { return null; }
            if (parameterType.indexOf("Collection(") === 0) {
                var innerType = parameterType.substring("Collection(".length, parameterType.length - 1);
                if (!Array.isArray(rawValue)) {
                    if (rawValue && Array.isArray(rawValue.value)) { rawValue = rawValue.value; }
                    else if (DRB.Utilities.HasValue(rawValue)) { rawValue = [rawValue]; }
                    else { rawValue = []; }
                }
                var converted = [];
                rawValue.forEach(function (entry) {
                    var singleValue = window.__drbConvertCapturedDataverseSingleValue(innerType, entry);
                    if (typeof singleValue !== "undefined" && singleValue !== null) {
                        converted.push(singleValue);
                    }
                });
                return converted;
            }

            return window.__drbConvertCapturedDataverseSingleValue(parameterType, rawValue);
        } catch (convertError) {
            console.error(convertError);
            return null;
        }
    };

    window.__drbConvertCapturedDataverseSingleValue = function (parameterType, rawValue) {
        if (!DRB.Utilities.HasValue(parameterType)) { return null; }
        if (!DRB.Utilities.HasValue(rawValue)) { return null; }

        var enumType = DRB.Utilities.GetRecordById(DRB.Metadata.DataverseEnumTypes, parameterType);
        if (DRB.Utilities.HasValue(enumType)) {
            if (enumType.IsFlags === true) {
                if (Array.isArray(rawValue)) {
                    var members = [];
                    rawValue.forEach(function (memberValue) {
                        members.push({ value: memberValue });
                    });
                    return { members: members };
                }
                return { memberValue: window.__drbCoerceNumber(rawValue) };
            }
            return { memberValue: window.__drbCoerceNumber(rawValue) };
        }

        if (parameterType === "mscrm.crmbaseentity") {
            return window.__drbConvertCapturedDataverseEntityValue(null, rawValue);
        }

        if (parameterType.indexOf("mscrm.") === 0) {
            var logicalName = parameterType.substring(6);
            var table = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, logicalName);
            if (DRB.Utilities.HasValue(table)) {
                return window.__drbConvertCapturedDataverseEntityValue(logicalName, rawValue);
            }
            return null; // unsupported complex type
        }

        switch (parameterType) {
            case "Edm.Boolean":
                return window.__drbCoerceBoolean(rawValue);
            case "Edm.Guid":
            case "Edm.String":
                return rawValue.toString();
            case "Edm.Int32":
            case "Edm.Int64":
                return window.__drbCoerceNumber(rawValue);
            case "Edm.Decimal":
            case "Edm.Double":
                return window.__drbCoerceNumber(rawValue, true);
            case "Edm.DateTimeOffset":
                return rawValue;
            default:
                return rawValue;
        }
    };

    window.__drbConvertCapturedDataverseEntityValue = function (explicitLogicalName, rawValue) {
        if (!DRB.Utilities.HasValue(rawValue)) { return null; }

        var explicitName = explicitLogicalName ? explicitLogicalName.toLowerCase() : null;
        if (typeof rawValue === "string") {
            if (!DRB.Utilities.HasValue(explicitName)) { return null; }
            var primaryIdFallback = window.__drbResolvePrimaryIdAttribute(explicitName);
            return { id: rawValue, entityType: explicitName, primaryIdField: primaryIdFallback };
        }

        var logicalName = explicitName || window.__drbInferLogicalNameFromCapturedValue(rawValue);
        if (!DRB.Utilities.HasValue(logicalName)) { return null; }
        var primaryId = window.__drbResolvePrimaryIdAttribute(logicalName);
        var idValue = null;
        if (DRB.Utilities.HasValue(primaryId)) {
            idValue = rawValue[primaryId] || rawValue[primaryId.toLowerCase()];
        }

        if (!DRB.Utilities.HasValue(idValue)) {
            Object.keys(rawValue).forEach(function (key) {
                if (idValue) { return; }
                if (key.toLowerCase().endsWith("id") && DRB.Utilities.HasValue(rawValue[key])) {
                    idValue = rawValue[key];
                    primaryId = key;
                }
            });
        }

        if (!DRB.Utilities.HasValue(idValue)) { return null; }
        return { id: idValue, entityType: logicalName, primaryIdField: primaryId || (logicalName + "id") };
    };

    window.__drbGetCapturedDataverseBindingInfo = function () {
        var logicalName = "";
        var entitySetName = "";
        var primaryId = "";

        if (DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) && DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) {
            var configuration = DRB.Metadata.CurrentNode.data.configuration || {};
            var configuredPrimary = configuration.primaryEntity;
            if (DRB.Utilities.HasValue(configuredPrimary)) {
                if (typeof configuredPrimary === "string") { logicalName = configuredPrimary; }
                else {
                    if (DRB.Utilities.HasValue(configuredPrimary.logicalName)) { logicalName = configuredPrimary.logicalName; }
                    if (DRB.Utilities.HasValue(configuredPrimary.entitySetName)) { entitySetName = configuredPrimary.entitySetName; }
                }
            }
            if (!DRB.Utilities.HasValue(logicalName) && DRB.Utilities.HasValue(configuration.primaryEntityLogicalName)) {
                logicalName = configuration.primaryEntityLogicalName;
            }
            if (!DRB.Utilities.HasValue(entitySetName) && DRB.Utilities.HasValue(configuration.primaryEntitySetName)) {
                entitySetName = configuration.primaryEntitySetName;
            }
            if (DRB.Utilities.HasValue(configuration.primaryId)) { primaryId = configuration.primaryId; }
        }

        var cachedBinding = null;
        if (DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) && DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) {
            cachedBinding = DRB.Metadata.CurrentNode.data.__capturedDataverseBinding;
        }

        if (DRB.Utilities.HasValue(cachedBinding)) {
            if (!DRB.Utilities.HasValue(logicalName) && DRB.Utilities.HasValue(cachedBinding.logicalName)) { logicalName = cachedBinding.logicalName; }
            if (!DRB.Utilities.HasValue(entitySetName) && DRB.Utilities.HasValue(cachedBinding.entitySetName)) { entitySetName = cachedBinding.entitySetName; }
            if (!DRB.Utilities.HasValue(primaryId) && DRB.Utilities.HasValue(cachedBinding.primaryId)) { primaryId = cachedBinding.primaryId; }
        }

        return { logicalName: logicalName || "", entitySetName: entitySetName || "", primaryId: primaryId || "" };
    };

    window.__drbDeriveDataverseBindingValue = function (parameter) {
        try {
            if (!DRB.Utilities.HasValue(parameter)) { return null; }
            var parameterName = DRB.Utilities.HasValue(parameter.Name) ? parameter.Name.toLowerCase() : "";
            if (parameterName.length === 0) { return null; }

            var supportedNames = ["entity", "target"];
            if (supportedNames.indexOf(parameterName) === -1) { return null; }

            var binding = window.__drbGetCapturedDataverseBindingInfo();
            if (!DRB.Utilities.HasValue(binding.logicalName) || !DRB.Utilities.HasValue(binding.primaryId)) { return null; }

            var logicalName = binding.logicalName.toLowerCase();
            var parameterType = DRB.Utilities.HasValue(parameter.Type) ? parameter.Type.toLowerCase() : "";
            var expectedType = "mscrm." + logicalName;
            if (parameterType.length > 0 && parameterType !== expectedType && parameterType !== "mscrm.crmbaseentity") { return null; }

            var primaryIdValue = binding.primaryId.toString().replace(/[{}]/g, "");
            var primaryIdField = window.__drbResolvePrimaryIdAttribute(logicalName);

            return {
                id: primaryIdValue,
                entityType: logicalName,
                primaryIdField: primaryIdField || (logicalName + "id")
            };
        } catch (bindingError) {
            console.error(bindingError);
            return null;
        }
    };

    window.__drbInferLogicalNameFromCapturedValue = function (rawValue) {
        if (!DRB.Utilities.HasValue(rawValue)) { return null; }
        if (DRB.Utilities.HasValue(rawValue["@odata.type"])) {
            var typeName = rawValue["@odata.type"]; // Microsoft.Dynamics.CRM.logical
            if (DRB.Utilities.HasValue(typeName) && typeName.indexOf("Microsoft.Dynamics.CRM.") > -1) {
                return typeName.substring(typeName.lastIndexOf('.') + 1).toLowerCase();
            }
        }
        return null;
    };

    window.__drbResolvePrimaryIdAttribute = function (logicalName) {
        if (!DRB.Utilities.HasValue(logicalName)) { return null; }
        var table = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, logicalName);
        if (DRB.Utilities.HasValue(table)) { return table.PrimaryIdAttribute; }
        return logicalName + "id";
    };

    window.__drbCoerceBoolean = function (rawValue) {
        if (typeof rawValue === "boolean") { return rawValue; }
        if (typeof rawValue === "string") {
            var lowered = rawValue.toLowerCase();
            return lowered === "true" || lowered === "1";
        }
        return Boolean(rawValue);
    };

    window.__drbCoerceNumber = function (rawValue, allowFloat) {
        if (typeof rawValue === "number") { return rawValue; }
        var parsed = allowFloat === true ? parseFloat(rawValue) : parseInt(rawValue, 10);
        if (isNaN(parsed)) { return null; }
        return parsed;
    };

    window.__drbPopulateCapturedRequest = function (nodeId, payload) {
        if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || DRB.Metadata.CurrentNode.id !== nodeId) { return; }

        if (payload.__drbMessageShown !== true) { payload.__drbMessageShown = false; }
        var showCapturedMessage = function () {
            DRB.UI.ShowMessage("Captured request added to REST Builder.");
            setTimeout(function () { DRB.UI.HideLoading(); }, 1200);
        };

        var resolvedRequestType = window.__drbResolveCapturedRequestType(payload);
        var isDataverseRequest = window.__drbIsDataverseRequestType(resolvedRequestType);
        if (isDataverseRequest) {
            window.__drbCacheCapturedDataverseParameters(nodeId, payload);
        }

        if (DRB.Utilities.HasValue(resolvedRequestType)) {
            payload.requestType = resolvedRequestType;
            $("#" + DRB.DOM.RequestType.Dropdown.Id).val(resolvedRequestType).change();
        }

        if (isDataverseRequest && window.__drbIsDataverseMetadataReady(resolvedRequestType) !== true) {
            payload.__drbMetadataDeferred = true;
            window.__drbDeferCapturedDataverseRequest(resolvedRequestType, nodeId, payload);
            if (payload.__drbMessageShown !== true) {
                showCapturedMessage();
                payload.__drbMessageShown = true;
            }
            return;
        }

        var applyEntity = function () {
            if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || DRB.Metadata.CurrentNode.id !== nodeId) { return; }

            var tableDropdown = $("#" + DRB.DOM.Table.Dropdown.Id);
            if (tableDropdown.length > 0) {
                var targetLogical = DRB.Utilities.HasValue(payload.primaryEntityLogicalName) ? payload.primaryEntityLogicalName : "";
                if (DRB.Utilities.HasValue(targetLogical)) {
                    tableDropdown.val(targetLogical);
                    if (tableDropdown.val() !== targetLogical && DRB.Utilities.HasValue(payload.entitySetName)) {
                        var fallbackTable = window.__drbFindTableByEntitySet(payload.entitySetName);
                        if (DRB.Utilities.HasValue(fallbackTable)) { targetLogical = fallbackTable.LogicalName; }
                        tableDropdown.val(targetLogical);
                    }
                    if (tableDropdown.val() === targetLogical) { tableDropdown.change(); }
                } else if (DRB.Utilities.HasValue(payload.entitySetName)) {
                    var table = window.__drbFindTableByEntitySet(payload.entitySetName);
                    if (DRB.Utilities.HasValue(table)) {
                        tableDropdown.val(table.LogicalName).change();
                    }
                }
            }

            if (DRB.Utilities.HasValue(payload.primaryId)) {
                var primaryInput = $("#" + DRB.DOM.PrimaryId.Input.Id);
                if (primaryInput.length > 0) {
                    primaryInput
                        .val(payload.primaryId)
                        .trigger("input")
                        .trigger("change");
                }
            }
        };

        setTimeout(applyEntity, 350);

        if (window.__drbIsDataverseRequestType(resolvedRequestType) && DRB.Utilities.HasValue(payload.dataverseOperationName)) {
            window.__drbScheduleCapturedDataverseExecuteSelection(nodeId, payload, 0);
        }

        if ((payload.requestType === "create" || payload.requestType === "update") && payload.bodyIsBinary !== true) {
            window.__drbScheduleCapturedFieldPopulation(nodeId, payload, 0);
        }

        if (payload.requestType === "retrievesingle" || payload.requestType === "retrievemultiple") {
            window.__drbScheduleCapturedSelectPopulation(nodeId, payload, 0);
        }

        if (payload.requestType === "retrievemultiple") {
            window.__drbScheduleCapturedExpandPopulation(nodeId, payload, 0);
            window.__drbScheduleCapturedOrderByPopulation(nodeId, payload, 0);
        }

        if (payload.requestType === "predefinedquery" && DRB.Utilities.HasValue(payload.fetchXml)) {
            window.__drbScheduleCapturedFetchXmlPopulation(nodeId, payload, 0);
        }

        window.__drbApplyCapturedQueryOverrides(nodeId, payload);
        if (payload.__drbMessageShown !== true) {
            showCapturedMessage();
            payload.__drbMessageShown = true;
        }
    };

    window.__drbApplyCapturedQueryOverrides = function (nodeId, payload) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
            var currentNode = DRB.Metadata.CurrentNode;
            if (!DRB.Utilities.HasValue(currentNode) || currentNode.id !== nodeId) { return; }
            if (!DRB.Utilities.HasValue(currentNode.data)) { return; }
            var configuration = currentNode.data.configuration || {};
            var overrides = configuration.capturedQueryOverrides || {};
            var overridesChanged = false;

            var filterValue = window.__drbFindQueryValue(payload, "$filter");
            if (DRB.Utilities.HasValue(filterValue)) {
                overrides.filter = filterValue.trim();
                overridesChanged = true;
            }

            var expandValue = window.__drbFindQueryValue(payload, "$expand");
            if (DRB.Utilities.HasValue(expandValue)) {
                overrides.expand = expandValue.trim();
                overridesChanged = true;
            }

            var orderValue = window.__drbFindQueryValue(payload, "$orderby");
            if (DRB.Utilities.HasValue(orderValue)) {
                overrides.orderby = orderValue.trim();
                overridesChanged = true;
            }

            if (Object.keys(overrides).length > 0) {
                configuration.capturedQueryOverrides = overrides;
                currentNode.data.configuration = configuration;
                if (overridesChanged === true && payload.requestType === "retrievemultiple" && typeof DRB.Logic.RetrieveMultiple.RenderCapturedFilterNotice === "function") {
                    setTimeout(function () {
                        try { DRB.Logic.RetrieveMultiple.RenderCapturedFilterNotice(); } catch { }
                    }, 0);
                }
            }
        } catch (overrideError) {
            console.error(overrideError);
        }
    };

    window.__drbScheduleCapturedFieldPopulation = function (nodeId, payload, attempt) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
            if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || DRB.Metadata.CurrentNode.id !== nodeId) {
                window.__drbClearPendingFieldInjection(nodeId);
                return;
            }
            if (payload.bodyIsBinary === true) {
                window.__drbClearPendingFieldInjection(nodeId);
                return;
            }
            if (DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data.__capturedFieldsApplied)) { return; }
            if (!DRB.Utilities.HasValue(payload.__drbFieldPopulateStart)) {
                payload.__drbFieldPopulateStart = Date.now();
            }

            if (!window.__drbCanPopulateCapturedFields()) {
                window.__drbTrackPendingFieldInjection(nodeId, payload, attempt);
                var elapsed = Date.now() - payload.__drbFieldPopulateStart;
                var maxWait = 120000;
                if (elapsed > maxWait) {
                    window.__drbClearPendingFieldInjection(nodeId);
                    return;
                }
                var backoff = Math.min(500 + (attempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedFieldPopulation(nodeId, payload, attempt + 1); }, backoff);
                return;
            }

            window.__drbClearPendingFieldInjection(nodeId);
            delete payload.__drbFieldPopulateStart;
            var parsedBody = window.__drbParseCapturedRequestBody(payload);
            if (!DRB.Utilities.HasValue(parsedBody)) { return; }
            var attributes = window.__drbExtractCapturedAttributes(parsedBody);
            if (!Array.isArray(attributes) || attributes.length === 0) { return; }

            DRB.Metadata.CurrentNode.data.__capturedFieldsApplied = true;
            var columnType = payload.requestType === "update" ? "IsValidForUpdate" : "IsValidForCreate";
            window.__drbApplyCapturedFieldValues(attributes, columnType, "setFields");
        } catch (scheduleError) {
            console.error(scheduleError);
        }
    };

    window.__drbCanPopulateCapturedFields = function () {
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode)) { return false; }
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentColumns) || DRB.Metadata.CurrentColumns.length === 0) { return false; }
        var addButtonId = DRB.DOM.SetColumns.AddButton.Id + "setFields";
        return $("#" + addButtonId).length > 0;
    };

    window.__drbTrackPendingFieldInjection = function (nodeId, payload, attempt) {
        if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
        if (!window.__drbPendingFieldInjections) { window.__drbPendingFieldInjections = {}; }
        window.__drbPendingFieldInjections[nodeId] = { payload: payload, attempt: attempt };
    };

    window.__drbClearPendingFieldInjection = function (nodeId) {
        if (!window.__drbPendingFieldInjections) { return; }
        if (window.__drbPendingFieldInjections[nodeId]) {
            delete window.__drbPendingFieldInjections[nodeId];
        }
    };

    window.__drbNotifyCapturedFieldsReady = function () {
        if (!window.__drbPendingFieldInjections) { return; }
        var pendingIds = Object.keys(window.__drbPendingFieldInjections);
        pendingIds.forEach(function (nodeId) {
            var entry = window.__drbPendingFieldInjections[nodeId];
            if (!entry || !entry.payload) { return; }
            window.__drbScheduleCapturedFieldPopulation(nodeId, entry.payload, (entry.attempt || 0) + 1);
        });
    };

    window.__drbParseCapturedRequestBody = function (payload) {
        if (!DRB.Utilities.HasValue(payload)) { return null; }
        var raw = payload.body;
        if (!DRB.Utilities.HasValue(raw) && DRB.Utilities.HasValue(payload.bodyBase64)) {
            try { raw = window.atob(payload.bodyBase64); }
            catch { raw = null; }
        }

        if (!DRB.Utilities.HasValue(raw) || typeof raw !== "string") { return null; }
        raw = raw.trim();
        if (raw.length === 0) { return null; }

        try {
            return JSON.parse(raw);
        } catch {
            return null;
        }
    };

    window.__drbExtractCapturedAttributes = function (bodyObject) {
        var attributes = [];
        try {
            Object.keys(bodyObject).forEach(function (key) {
                if (!Object.prototype.hasOwnProperty.call(bodyObject, key)) { return; }
                var value = bodyObject[key];
                if (!DRB.Utilities.HasValue(key)) { return; }
                if (key.indexOf("@odata.") > -1) {
                    if (key.toLowerCase().endsWith("@odata.bind")) {
                        var logicalName = key.split("@")[0];
                        attributes.push({ logicalName: logicalName, typeHint: "lookup", raw: value });
                    }
                    return;
                }
                if (key.indexOf("@") > -1) { return; }
                attributes.push({ logicalName: key, value: value });
            });
        } catch { }
        return attributes;
    };

    window.__drbApplyCapturedFieldValues = function (attributes, columnType, metadataPath) {
        if (!Array.isArray(attributes) || attributes.length === 0) { return; }
        attributes.forEach(function (attribute) {
            window.__drbUpsertCapturedField(attribute, columnType, metadataPath);
        });
    };

    window.__drbUpsertCapturedField = function (attribute, columnType, metadataPath) {
        try {
            if (!DRB.Utilities.HasValue(attribute) || !DRB.Utilities.HasValue(attribute.logicalName)) { return; }
            var logicalName = attribute.logicalName.toLowerCase();
            var column = DRB.Utilities.GetRecordById(DRB.Metadata.CurrentColumns, logicalName);
            if (!DRB.Utilities.HasValue(column)) { return; }
            if (column[columnType] !== true) { return; }

            var uniqueIndex = window.__drbFindExistingSetFieldRow(metadataPath, logicalName);
            if (!DRB.Utilities.HasValue(uniqueIndex)) {
                DRB.Logic.AddColumn(columnType, "SetColumns", metadataPath);
                uniqueIndex = window.__drbGetUniqueIndexForLastColumn(metadataPath);
            }
            if (!DRB.Utilities.HasValue(uniqueIndex)) { return; }

            window.__drbSelectColumnForCapturedField(uniqueIndex, logicalName, function () {
                window.__drbSetCapturedFieldValue(uniqueIndex, column, attribute, 0);
            });
        } catch (upsertError) {
            console.error(upsertError);
        }
    };

    window.__drbGetMetadataList = function (metadataPath) {
        var ref = DRB.Metadata;
        if (!DRB.Utilities.HasValue(ref)) { return []; }
        var segments = metadataPath.split("_");
        for (var i = 0; i < segments.length; i++) {
            var segment = segments[i];
            if (!DRB.Utilities.HasValue(segment)) { continue; }
            if (isNaN(parseInt(segment))) {
                if (ref.hasOwnProperty(segment)) { ref = ref[segment]; }
                else { return []; }
            } else {
                var index = parseInt(segment);
                if (!Array.isArray(ref)) { return []; }
                var found = null;
                ref.forEach(function (entry) { if (entry.Id === index) { found = entry; } });
                if (!DRB.Utilities.HasValue(found)) { return []; }
                ref = found;
            }
        }
        if (!Array.isArray(ref)) { return []; }
        return ref;
    };

    window.__drbFindExistingSetFieldRow = function (metadataPath, logicalName) {
        var list = window.__drbGetMetadataList(metadataPath);
        if (!Array.isArray(list)) { return null; }
        var match = null;
        list.forEach(function (entry) {
            if (DRB.Utilities.HasValue(entry.Value) && entry.Value.logicalName === logicalName) {
                match = metadataPath + "_" + entry.Id;
            }
        });
        return match;
    };

    window.__drbGetUniqueIndexForLastColumn = function (metadataPath) {
        var list = window.__drbGetMetadataList(metadataPath);
        if (!Array.isArray(list) || list.length === 0) { return null; }
        return metadataPath + "_" + list[list.length - 1].Id;
    };

    window.__drbSelectColumnForCapturedField = function (uniqueIndex, logicalName, callback, attempt) {
        var dropdownId = DRB.DOM.SetColumns.Dropdown.Id + uniqueIndex;
        var dropdown = $("#" + dropdownId);
        var currentAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
        if (dropdown.length === 0) {
            if (currentAttempt >= 40) { return; }
            setTimeout(function () { window.__drbSelectColumnForCapturedField(uniqueIndex, logicalName, callback, currentAttempt + 1); }, 150);
            return;
        }

        if (dropdown.val() !== logicalName) {
            dropdown.val(logicalName).trigger("change");
        }

        setTimeout(function () {
            if (typeof callback === "function") { callback(); }
        }, 200);
    };

    window.__drbSetCapturedFieldValue = function (uniqueIndex, column, attribute, attempt) {
        var maxAttempts = 40;
        var currentAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
        if (currentAttempt >= maxAttempts) { return; }
        var baseId = DRB.DOM.SetColumns.ControlValue.Id + uniqueIndex;
        var value = attribute.typeHint === "lookup" ? attribute.raw : attribute.value;
        var retry = function () {
            setTimeout(function () { window.__drbSetCapturedFieldValue(uniqueIndex, column, attribute, currentAttempt + 1); }, 150);
        };

        switch (column.AttributeType) {
            case "String":
            case "Memo":
            case "EntityName":
            case "Uniqueidentifier":
            case "Integer":
            case "BigInt":
            case "Decimal":
            case "Double":
            case "Money": {
                var inputId = "#txt_" + baseId;
                var input = $(inputId);
                if (input.length === 0) { retry(); return; }
                var normalized = window.__drbNormalizePrimitiveValue(value);
                input.val(normalized).trigger("input").trigger("change");
                return;
            }
            case "ManagedProperty":
            case "Boolean":
            case "Picklist":
            case "State":
            case "Status": {
                var dropdownId = "#cbx1_" + baseId;
                var dropdown = $(dropdownId);
                if (dropdown.length === 0) { retry(); return; }
                var optionValue = window.__drbNormalizeOptionValue(value);
                dropdown.val(optionValue).trigger("change");
                if (typeof dropdown.selectpicker === "function") {
                    dropdown.selectpicker("refresh");
                }
                return;
            }
            case "MultiPicklist": {
                var multiId = "#cbxm_" + baseId;
                var multi = $(multiId);
                if (multi.length === 0) { retry(); return; }
                var multiValues = window.__drbNormalizeMultiOptionValue(value);
                if (typeof multi.selectpicker === "function") {
                    multi.selectpicker("val", multiValues);
                }
                multi.val(multiValues).trigger("change");
                return;
            }
            case "Lookup":
            case "Owner":
            case "Customer": {
                var parsed = window.__drbParseLookupBinding(value);
                if (!DRB.Utilities.HasValue(parsed)) { return; }
                var guidInput = $("#txt2_" + baseId);
                if (guidInput.length === 0) { retry(); return; }
                guidInput.val(parsed.id).trigger("change");
                var targetDropdown = $("#cbx2_" + baseId);
                if (targetDropdown.length > 0 && DRB.Utilities.HasValue(parsed.logicalName)) {
                    targetDropdown.val(parsed.logicalName).trigger("change");
                    if (typeof targetDropdown.selectpicker === "function") {
                        targetDropdown.selectpicker("refresh");
                    }
                }
                return;
            }
            case "DateTime": {
                var dateId = "#txtd_" + baseId;
                var dateInput = $(dateId);
                if (dateInput.length === 0) { retry(); return; }
                var normalizedDate = window.__drbNormalizePrimitiveValue(value);
                dateInput.val(normalizedDate).trigger("change");
                return;
            }
            default:
                return;
        }
    };

    window.__drbNormalizePrimitiveValue = function (value) {
        if (value === null || value === undefined) { return ""; }
        if (typeof value === "object") { return ""; }
        return value.toString();
    };

    window.__drbNormalizeOptionValue = function (value) {
        if (value === null || value === undefined) { return ""; }
        if (typeof value === "boolean") { return value ? "true" : "false"; }
        if (typeof value === "number") { return value.toString(); }
        if (typeof value === "object" && DRB.Utilities.HasValue(value.Value)) { return value.Value.toString(); }
        return value.toString();
    };

    window.__drbNormalizeMultiOptionValue = function (value) {
        if (value === null || value === undefined) { return []; }
        if (Array.isArray(value)) { return value.map(function (v) { return v.toString(); }); }
        return [value.toString()];
    };

    window.__drbParseLookupBinding = function (raw) {
        if (!DRB.Utilities.HasValue(raw) || typeof raw !== "string") { return null; }
        var match = raw.match(/\/([^\/()]+)\(([^)]+)\)/);
        if (!DRB.Utilities.HasValue(match) || match.length < 3) { return null; }
        var entitySet = match[1];
        var recordId = match[2].replace(/\{/g, "").replace(/\}/g, "");
        var table = window.__drbFindTableByEntitySet(entitySet);
        var logicalName = DRB.Utilities.HasValue(table) ? table.LogicalName : "";
        return { entitySetName: entitySet, logicalName: logicalName, id: recordId };
    };

    window.__drbFindTableByEntitySet = function (entitySetName) {
        if (!DRB.Utilities.HasValue(entitySetName) || !Array.isArray(DRB.Metadata.Tables)) { return null; }
        var target = null;
        var comparer = entitySetName.toLowerCase();
        DRB.Metadata.Tables.forEach(function (table) {
            if (DRB.Utilities.HasValue(table.EntitySetName) && table.EntitySetName.toLowerCase() === comparer) {
                target = table;
            }
        });
        return target;
    };

    window.__drbScheduleCapturedSelectPopulation = function (nodeId, payload, attempt, cachedColumns) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
            var currentNode = DRB.Metadata.CurrentNode;
            if (!DRB.Utilities.HasValue(currentNode) || currentNode.id !== nodeId) {
                window.__drbClearPendingSelectInjection(nodeId);
                return;
            }
            if (DRB.Utilities.HasValue(currentNode.data.__capturedSelectApplied)) {
                window.__drbClearPendingSelectInjection(nodeId);
                return;
            }

            var currentAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;

            var selectColumns = Array.isArray(cachedColumns) ? cachedColumns : window.__drbExtractCapturedSelectColumns(payload);
            if (!Array.isArray(selectColumns) || selectColumns.length === 0) {
                window.__drbClearPendingSelectInjection(nodeId);
                return;
            }

            if (!DRB.Utilities.HasValue(payload.__drbSelectPopulateStart)) {
                payload.__drbSelectPopulateStart = Date.now();
            }
            var maxWait = 120000;

            if (!window.__drbCanPopulateCapturedSelects(payload)) {
                window.__drbTrackPendingSelectInjection(nodeId, payload, currentAttempt, selectColumns);
                var elapsed = Date.now() - payload.__drbSelectPopulateStart;
                if (elapsed > maxWait) {
                    window.__drbClearPendingSelectInjection(nodeId);
                    return;
                }
                var backoff = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedSelectPopulation(nodeId, payload, currentAttempt + 1, selectColumns); }, backoff);
                return;
            }

            var applied = window.__drbApplyCapturedSelectColumns(selectColumns);
            if (applied !== true) {
                window.__drbTrackPendingSelectInjection(nodeId, payload, currentAttempt, selectColumns);
                var attemptElapsed = Date.now() - payload.__drbSelectPopulateStart;
                if (attemptElapsed > maxWait) {
                    window.__drbClearPendingSelectInjection(nodeId);
                    delete payload.__drbSelectPopulateStart;
                    return;
                }
                var retryDelay = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedSelectPopulation(nodeId, payload, currentAttempt + 1, selectColumns); }, retryDelay);
                return;
            }

            window.__drbClearPendingSelectInjection(nodeId);
            delete payload.__drbSelectPopulateStart;
            currentNode.data.__capturedSelectApplied = true;
        } catch (selectError) {
            console.error(selectError);
        }
    };

    window.__drbCanPopulateCapturedSelects = function (payload) {
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode)) { return false; }
        var currentData = DRB.Metadata.CurrentNode.data || {};
        var configuration = DRB.Utilities.HasValue(currentData.configuration) ? currentData.configuration : null;
        if (!DRB.Utilities.HasValue(configuration)) { return false; }
        if (DRB.Utilities.HasValue(payload)) {
            var expectedLogical = DRB.Utilities.HasValue(payload.primaryEntityLogicalName) ? payload.primaryEntityLogicalName.toLowerCase() : "";
            var expectedEntitySet = DRB.Utilities.HasValue(payload.entitySetName) ? payload.entitySetName.toLowerCase() : "";
            if (expectedLogical.length > 0 || expectedEntitySet.length > 0) {
                var configuredLogical = "";
                var configuredEntitySet = "";
                if (DRB.Utilities.HasValue(configuration.primaryEntity)) {
                    var primaryEntity = configuration.primaryEntity;
                    if (typeof primaryEntity === "string") {
                        configuredLogical = primaryEntity.toLowerCase();
                    } else {
                        if (DRB.Utilities.HasValue(primaryEntity.logicalName)) { configuredLogical = primaryEntity.logicalName.toLowerCase(); }
                        if (DRB.Utilities.HasValue(primaryEntity.entitySetName)) { configuredEntitySet = primaryEntity.entitySetName.toLowerCase(); }
                    }
                }
                if (!DRB.Utilities.HasValue(configuredLogical) && DRB.Utilities.HasValue(configuration.primaryEntityLogicalName)) {
                    configuredLogical = configuration.primaryEntityLogicalName.toLowerCase();
                }
                if (configuredLogical.length === 0 && configuredEntitySet.length === 0) { return false; }
                if (expectedLogical.length > 0 && configuredLogical !== expectedLogical) {
                    if (!(expectedEntitySet.length > 0 && configuredEntitySet === expectedEntitySet)) { return false; }
                } else if (expectedLogical.length === 0 && expectedEntitySet.length > 0 && configuredEntitySet !== expectedEntitySet) {
                    return false;
                }
            }
        }
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentColumns) || DRB.Metadata.CurrentColumns.length === 0) { return false; }
        var dropdown = $("#" + DRB.DOM.Columns.Dropdown.Id);
        if (dropdown.length === 0) { return false; }
        return true;
    };

    window.__drbExtractCapturedSelectColumns = function (payload) {
        if (!DRB.Utilities.HasValue(payload)) { return []; }
        var raw = window.__drbFindQueryValue(payload, "$select");
        if (!DRB.Utilities.HasValue(raw)) { return []; }
        var normalized = raw.split(',').map(function (entry) { return window.__drbNormalizeSelectColumnName(entry); }).filter(function (entry) { return entry.length > 0; });
        if (normalized.length === 0) { return []; }
        var deduped = [];
        normalized.forEach(function (entry) {
            var lowered = entry.toLowerCase();
            if (deduped.indexOf(lowered) === -1) { deduped.push(lowered); }
        });
        return deduped;
    };

    window.__drbNormalizeSelectColumnName = function (columnName) {
        if (!DRB.Utilities.HasValue(columnName)) { return ""; }
        var trimmed = columnName.trim();
        if (trimmed.length === 0) { return ""; }
        var lowered = trimmed.toLowerCase();
        if (lowered.charAt(0) === '_' && lowered.endsWith('_value') && lowered.length > 7) {
            return lowered.substring(1, lowered.length - 6);
        }
        return lowered;
    };

    window.__drbApplyCapturedSelectColumns = function (columns) {
        if (!Array.isArray(columns) || columns.length === 0) { return false; }
        var dropdownId = DRB.DOM.Columns.Dropdown.Id;
        var dropdown = $("#" + dropdownId);
        if (dropdown.length === 0) { return false; }

        var resolved = [];
        columns.forEach(function (logicalName) {
            var column = window.__drbFindColumnByLogicalName(logicalName);
            if (DRB.Utilities.HasValue(column) && resolved.indexOf(column.LogicalName) === -1) {
                resolved.push(column.LogicalName);
            }
        });

        if (resolved.length === 0) { return false; }
        dropdown.val(resolved).trigger("change");
        if (typeof dropdown.selectpicker === "function") { dropdown.selectpicker("refresh"); }
        return true;
    };

    window.__drbTrackPendingSelectInjection = function (nodeId, payload, attempt, columns) {
        if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
        if (!window.__drbPendingSelectInjections) { window.__drbPendingSelectInjections = {}; }
        var cleanAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
        window.__drbPendingSelectInjections[nodeId] = { payload: payload, attempt: cleanAttempt, columns: columns };
    };

    window.__drbClearPendingSelectInjection = function (nodeId) {
        if (!window.__drbPendingSelectInjections) { return; }
        if (window.__drbPendingSelectInjections[nodeId]) {
            delete window.__drbPendingSelectInjections[nodeId];
        }
    };

    window.__drbNotifyCapturedSelectsReady = function () {
        if (!window.__drbPendingSelectInjections) { return; }
        Object.keys(window.__drbPendingSelectInjections).forEach(function (nodeId) {
            var entry = window.__drbPendingSelectInjections[nodeId];
            if (!entry || !entry.payload) { return; }
            window.__drbScheduleCapturedSelectPopulation(nodeId, entry.payload, (entry.attempt || 0) + 1, entry.columns);
        });
    };

    window.__drbScheduleCapturedExpandPopulation = function (nodeId, payload, attempt, cachedEntries) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
            var currentNode = DRB.Metadata.CurrentNode;
            if (!DRB.Utilities.HasValue(currentNode) || currentNode.id !== nodeId) {
                window.__drbClearPendingExpandInjection(nodeId);
                return;
            }
            if (DRB.Utilities.HasValue(currentNode.data.__capturedExpandApplied)) {
                window.__drbClearPendingExpandInjection(nodeId);
                return;
            }

            var currentAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
            var expandEntries = Array.isArray(cachedEntries) ? cachedEntries : window.__drbExtractCapturedExpandEntries(payload);
            if (!Array.isArray(expandEntries) || expandEntries.length === 0) {
                window.__drbClearPendingExpandInjection(nodeId);
                return;
            }

            if (!DRB.Utilities.HasValue(payload.__drbExpandPopulateStart)) {
                payload.__drbExpandPopulateStart = Date.now();
            }
            var maxWait = 120000;

            if (!window.__drbCanPopulateCapturedExpand()) {
                window.__drbTrackPendingExpandInjection(nodeId, payload, currentAttempt, expandEntries);
                var elapsed = Date.now() - payload.__drbExpandPopulateStart;
                if (elapsed > maxWait) {
                    window.__drbClearPendingExpandInjection(nodeId);
                    return;
                }
                var backoff = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedExpandPopulation(nodeId, payload, currentAttempt + 1, expandEntries); }, backoff);
                return;
            }

            var applied = window.__drbApplyCapturedExpand(expandEntries);
            if (applied !== true) {
                window.__drbTrackPendingExpandInjection(nodeId, payload, currentAttempt, expandEntries);
                var attemptElapsed = Date.now() - payload.__drbExpandPopulateStart;
                if (attemptElapsed > maxWait) {
                    window.__drbClearPendingExpandInjection(nodeId);
                    return;
                }
                var retryDelay = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedExpandPopulation(nodeId, payload, currentAttempt + 1, expandEntries); }, retryDelay);
                return;
            }

            window.__drbClearPendingExpandInjection(nodeId);
            delete payload.__drbExpandPopulateStart;
            currentNode.data.__capturedExpandApplied = true;
        } catch (expandError) {
            console.error(expandError);
        }
    };

    window.__drbCanPopulateCapturedExpand = function () {
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode)) { return false; }
        if (!Array.isArray(DRB.Metadata.Tables) || DRB.Metadata.Tables.length === 0) { return false; }
        var hasRelationships = Array.isArray(DRB.Metadata.CurrentOneToMany)
            || Array.isArray(DRB.Metadata.CurrentManyToOne)
            || Array.isArray(DRB.Metadata.CurrentManyToMany);
        if (!hasRelationships) { return false; }
        return true;
    };

    window.__drbExtractCapturedExpandEntries = function (payload) {
        if (!DRB.Utilities.HasValue(payload)) { return []; }
        var raw = window.__drbFindQueryValue(payload, "$expand");
        if (!DRB.Utilities.HasValue(raw)) { return []; }
        var entries = window.__drbSplitCapturedExpandEntries(raw);
        if (!Array.isArray(entries) || entries.length === 0) { return []; }
        var parsedEntries = [];
        entries.forEach(function (entry) {
            var parsed = window.__drbParseCapturedExpandEntry(entry);
            if (DRB.Utilities.HasValue(parsed)) { parsedEntries.push(parsed); }
        });
        return parsedEntries;
    };

    window.__drbSplitCapturedExpandEntries = function (rawValue) {
        var entries = [];
        if (!DRB.Utilities.HasValue(rawValue)) { return entries; }
        var buffer = '';
        var depth = 0;
        var inQuotes = false;
        for (var i = 0; i < rawValue.length; i++) {
            var char = rawValue[i];
            if (char === "'") {
                if (inQuotes === true && i + 1 < rawValue.length && rawValue[i + 1] === "'") {
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
                if (char === ',' && depth === 0) {
                    if (buffer.trim().length > 0) { entries.push(buffer.trim()); }
                    buffer = '';
                    continue;
                }
            }
            buffer += char;
        }
        if (buffer.trim().length > 0) { entries.push(buffer.trim()); }
        return entries;
    };

    window.__drbParseCapturedExpandEntry = function (entry) {
        if (!DRB.Utilities.HasValue(entry)) { return null; }
        var trimmed = entry.trim();
        if (trimmed.length === 0) { return null; }
        var name = trimmed;
        var optionsText = null;
        var openIndex = trimmed.indexOf('(');
        if (openIndex > -1 && trimmed.endsWith(')')) {
            name = trimmed.substring(0, openIndex).trim();
            optionsText = trimmed.substring(openIndex + 1, trimmed.length - 1);
        }
        if (name.length === 0) { return null; }
        var selectColumns = [];
        if (DRB.Utilities.HasValue(optionsText)) {
            var selectValue = window.__drbFindExpandOptionValue(optionsText, "$select");
            if (DRB.Utilities.HasValue(selectValue)) {
                selectColumns = selectValue.split(',').map(function (value) { return window.__drbNormalizeSelectColumnName(value); })
                    .filter(function (value) { return value.length > 0; });
            }
        }
        return { name: name, selectColumns: selectColumns };
    };

    window.__drbFindExpandOptionValue = function (optionsText, key) {
        if (!DRB.Utilities.HasValue(optionsText) || !DRB.Utilities.HasValue(key)) { return null; }
        var segments = window.__drbSplitCapturedExpandOptions(optionsText);
        var target = key.toLowerCase();
        for (var i = 0; i < segments.length; i++) {
            var segment = segments[i];
            if (!DRB.Utilities.HasValue(segment)) { continue; }
            var splitterIndex = segment.indexOf('=');
            if (splitterIndex === -1) { continue; }
            var optionKey = segment.substring(0, splitterIndex).trim().toLowerCase();
            if (optionKey === target) {
                return segment.substring(splitterIndex + 1).trim();
            }
        }
        return null;
    };

    window.__drbSplitCapturedExpandOptions = function (optionsText) {
        var entries = [];
        if (!DRB.Utilities.HasValue(optionsText)) { return entries; }
        var buffer = '';
        var depth = 0;
        var inQuotes = false;
        for (var i = 0; i < optionsText.length; i++) {
            var char = optionsText[i];
            if (char === "'") {
                if (inQuotes === true && i + 1 < optionsText.length && optionsText[i + 1] === "'") {
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
                if (char === ';' && depth === 0) {
                    if (buffer.trim().length > 0) { entries.push(buffer.trim()); }
                    buffer = '';
                    continue;
                }
            }
            buffer += char;
        }
        if (buffer.trim().length > 0) { entries.push(buffer.trim()); }
        return entries;
    };

    window.__drbFindRelationshipByExpandName = function (expandName) {
        if (!DRB.Utilities.HasValue(expandName)) { return null; }
        var comparer = expandName.toLowerCase();
        var relationshipSets = [
            { metadata: DRB.Metadata.CurrentOneToMany, configKey: "oneToMany" },
            { metadata: DRB.Metadata.CurrentManyToOne, configKey: "manyToOne" },
            { metadata: DRB.Metadata.CurrentManyToMany, configKey: "manyToMany" }
        ];
        for (var setIndex = 0; setIndex < relationshipSets.length; setIndex++) {
            var set = relationshipSets[setIndex];
            if (!Array.isArray(set.metadata)) { continue; }
            for (var relIndex = 0; relIndex < set.metadata.length; relIndex++) {
                var relationship = set.metadata[relIndex];
                if (!DRB.Utilities.HasValue(relationship)) { continue; }
                if (DRB.Utilities.HasValue(relationship.SchemaName) && relationship.SchemaName.toLowerCase() === comparer) {
                    return { relationship: relationship, configKey: set.configKey, metadata: set.metadata };
                }
                if (DRB.Utilities.HasValue(relationship.NavigationProperty) && relationship.NavigationProperty.toLowerCase() === comparer) {
                    return { relationship: relationship, configKey: set.configKey, metadata: set.metadata };
                }
            }
        }
        return null;
    };

    window.__drbFindColumnByCapturedExpandName = function (columns, name) {
        if (!Array.isArray(columns) || !DRB.Utilities.HasValue(name)) { return null; }
        var comparer = name.toLowerCase();
        for (var i = 0; i < columns.length; i++) {
            var column = columns[i];
            if (!DRB.Utilities.HasValue(column)) { continue; }
            if (DRB.Utilities.HasValue(column.ODataName) && column.ODataName.toLowerCase() === comparer) { return column; }
            if (DRB.Utilities.HasValue(column.LogicalName) && column.LogicalName.toLowerCase() === comparer) { return column; }
            if (DRB.Utilities.HasValue(column.SchemaName) && column.SchemaName.toLowerCase() === comparer) { return column; }
        }
        return null;
    };

    window.__drbApplyCapturedExpand = function (expandEntries) {
        if (!Array.isArray(expandEntries) || expandEntries.length === 0) { return false; }
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || !DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) { return false; }
        var configuration = DRB.Metadata.CurrentNode.data.configuration || {};
        var hasExisting = (Array.isArray(configuration.oneToMany) && configuration.oneToMany.length > 0)
            || (Array.isArray(configuration.manyToOne) && configuration.manyToOne.length > 0)
            || (Array.isArray(configuration.manyToMany) && configuration.manyToMany.length > 0);
        if (hasExisting === true) { return false; }

        var relValues = {
            oneToMany: [],
            manyToOne: [],
            manyToMany: []
        };

        expandEntries.forEach(function (entry) {
            if (!DRB.Utilities.HasValue(entry) || !DRB.Utilities.HasValue(entry.name)) { return; }
            var relationshipInfo = window.__drbFindRelationshipByExpandName(entry.name);
            if (!DRB.Utilities.HasValue(relationshipInfo) || !DRB.Utilities.HasValue(relationshipInfo.relationship)) { return; }
            var targetTable = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, relationshipInfo.relationship.TargetTable);
            if (!DRB.Utilities.HasValue(targetTable) || !Array.isArray(targetTable.Columns)) { return; }
            if (!Array.isArray(entry.selectColumns) || entry.selectColumns.length === 0) { return; }

            var selectedValues = [];
            entry.selectColumns.forEach(function (columnName) {
                var normalized = window.__drbNormalizeSelectColumnName(columnName);
                if (!DRB.Utilities.HasValue(normalized)) { return; }
                var column = window.__drbFindColumnByCapturedExpandName(targetTable.Columns, normalized);
                if (!DRB.Utilities.HasValue(column)) { return; }
                var value = relationshipInfo.relationship.SchemaName + "|" + column.LogicalName;
                if (selectedValues.indexOf(value) === -1) { selectedValues.push(value); }
            });

            if (selectedValues.length === 0) { return; }
            selectedValues.forEach(function (value) {
                if (relValues[relationshipInfo.configKey].indexOf(value) === -1) {
                    relValues[relationshipInfo.configKey].push(value);
                }
            });
        });

        var applied = false;
        if (relValues.oneToMany.length > 0) {
            configuration.oneToMany = DRB.Logic.ExportRelationships(relValues.oneToMany, DRB.Metadata.CurrentOneToMany);
            applied = true;
        }
        if (relValues.manyToOne.length > 0) {
            configuration.manyToOne = DRB.Logic.ExportRelationships(relValues.manyToOne, DRB.Metadata.CurrentManyToOne);
            applied = true;
        }
        if (relValues.manyToMany.length > 0) {
            configuration.manyToMany = DRB.Logic.ExportRelationships(relValues.manyToMany, DRB.Metadata.CurrentManyToMany);
            applied = true;
        }

        if (applied !== true) { return false; }
        DRB.Metadata.CurrentNode.data.configuration = configuration;
        if (typeof DRB.Logic.FillRelationships === "function") {
            DRB.Logic.FillRelationships();
        }
        if (configuration.capturedQueryOverrides && DRB.Utilities.HasValue(configuration.capturedQueryOverrides.expand)) {
            delete configuration.capturedQueryOverrides.expand;
        }
        return true;
    };

    window.__drbTrackPendingExpandInjection = function (nodeId, payload, attempt, entries) {
        if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
        if (!window.__drbPendingExpandInjections) { window.__drbPendingExpandInjections = {}; }
        var cleanAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
        window.__drbPendingExpandInjections[nodeId] = { payload: payload, attempt: cleanAttempt, entries: entries };
    };

    window.__drbClearPendingExpandInjection = function (nodeId) {
        if (!window.__drbPendingExpandInjections) { return; }
        if (window.__drbPendingExpandInjections[nodeId]) {
            delete window.__drbPendingExpandInjections[nodeId];
        }
    };

    window.__drbScheduleCapturedOrderByPopulation = function (nodeId, payload, attempt, cachedOrders) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
            var currentNode = DRB.Metadata.CurrentNode;
            if (!DRB.Utilities.HasValue(currentNode) || currentNode.id !== nodeId) {
                window.__drbClearPendingOrderInjection(nodeId);
                return;
            }
            if (DRB.Utilities.HasValue(currentNode.data.__capturedOrderApplied)) {
                window.__drbClearPendingOrderInjection(nodeId);
                return;
            }

            var currentAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
            var orderEntries = Array.isArray(cachedOrders) ? cachedOrders : window.__drbExtractCapturedOrderByColumns(payload);
            if (!Array.isArray(orderEntries) || orderEntries.length === 0) {
                window.__drbClearPendingOrderInjection(nodeId);
                return;
            }

            if (!DRB.Utilities.HasValue(payload.__drbOrderPopulateStart)) {
                payload.__drbOrderPopulateStart = Date.now();
            }
            var maxWait = 120000;

            if (!window.__drbCanPopulateCapturedOrderBy()) {
                window.__drbTrackPendingOrderInjection(nodeId, payload, currentAttempt, orderEntries);
                var elapsed = Date.now() - payload.__drbOrderPopulateStart;
                if (elapsed > maxWait) {
                    window.__drbClearPendingOrderInjection(nodeId);
                    return;
                }
                var backoff = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedOrderByPopulation(nodeId, payload, currentAttempt + 1, orderEntries); }, backoff);
                return;
            }

            var applied = window.__drbApplyCapturedOrderBy(orderEntries);
            if (applied !== true) {
                window.__drbTrackPendingOrderInjection(nodeId, payload, currentAttempt, orderEntries);
                var attemptElapsed = Date.now() - payload.__drbOrderPopulateStart;
                if (attemptElapsed > maxWait) {
                    window.__drbClearPendingOrderInjection(nodeId);
                    return;
                }
                var retryDelay = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedOrderByPopulation(nodeId, payload, currentAttempt + 1, orderEntries); }, retryDelay);
                return;
            }

            window.__drbClearPendingOrderInjection(nodeId);
            delete payload.__drbOrderPopulateStart;
            currentNode.data.__capturedOrderApplied = true;
        } catch (orderError) {
            console.error(orderError);
        }
    };

    window.__drbCanPopulateCapturedOrderBy = function () {
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode)) { return false; }
        if (!Array.isArray(DRB.Metadata.CurrentColumns) || DRB.Metadata.CurrentColumns.length === 0) { return false; }
        var table = $("#" + DRB.DOM.OrderColumns.Table.Id + "orderFields");
        if (table.length === 0) { return false; }
        return true;
    };

    window.__drbExtractCapturedOrderByColumns = function (payload) {
        if (!DRB.Utilities.HasValue(payload)) { return []; }
        var raw = window.__drbFindQueryValue(payload, "$orderby");
        if (!DRB.Utilities.HasValue(raw)) { return []; }
        var entries = raw.split(",").map(function (entry) { return entry.trim(); }).filter(function (entry) { return entry.length > 0; });
        if (entries.length === 0) { return []; }
        return entries.map(function (entry) {
            var normalized = entry.replace(/\s+/g, " ").trim();
            var parts = normalized.split(" ");
            var name = parts[0];
            var direction = parts.length > 1 ? parts[1].toLowerCase() : "asc";
            if (direction !== "desc") { direction = "asc"; }
            return { name: window.__drbNormalizeSelectColumnName(name), direction: direction };
        });
    };

    window.__drbFindColumnByOrderName = function (orderName) {
        if (!DRB.Utilities.HasValue(orderName) || !Array.isArray(DRB.Metadata.CurrentColumns)) { return null; }
        var comparer = orderName.toLowerCase();
        for (var index = 0; index < DRB.Metadata.CurrentColumns.length; index++) {
            var column = DRB.Metadata.CurrentColumns[index];
            if (!DRB.Utilities.HasValue(column)) { continue; }
            if (DRB.Utilities.HasValue(column.LogicalName) && column.LogicalName.toLowerCase() === comparer) { return column; }
            if (DRB.Utilities.HasValue(column.ODataName) && column.ODataName.toLowerCase() === comparer) { return column; }
            if (DRB.Utilities.HasValue(column.SchemaName) && column.SchemaName.toLowerCase() === comparer) { return column; }
        }
        return null;
    };

    window.__drbApplyCapturedOrderBy = function (orderEntries) {
        if (!Array.isArray(orderEntries) || orderEntries.length === 0) { return false; }
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) || !DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data)) { return false; }
        var configuration = DRB.Metadata.CurrentNode.data.configuration || {};
        var resolved = [];
        orderEntries.forEach(function (entry) {
            if (!DRB.Utilities.HasValue(entry) || !DRB.Utilities.HasValue(entry.name)) { return; }
            var column = window.__drbFindColumnByOrderName(entry.name);
            if (!DRB.Utilities.HasValue(column)) { return; }
            resolved.push({
                logicalName: column.LogicalName,
                schemaName: column.SchemaName,
                label: column.Name,
                type: column.AttributeType,
                oDataName: column.ODataName,
                value: entry.direction
            });
        });

        if (resolved.length === 0) { return false; }
        configuration.orderFields = resolved;
        DRB.Metadata.CurrentNode.data.configuration = configuration;
        if (typeof DRB.Logic.RetrieveMultiple.ConfigureOrderColumns === "function") {
            DRB.Logic.RetrieveMultiple.ConfigureOrderColumns();
        }
        return true;
    };

    window.__drbTrackPendingOrderInjection = function (nodeId, payload, attempt, orders) {
        if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
        if (!window.__drbPendingOrderInjections) { window.__drbPendingOrderInjections = {}; }
        var cleanAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
        window.__drbPendingOrderInjections[nodeId] = { payload: payload, attempt: cleanAttempt, orders: orders };
    };

    window.__drbClearPendingOrderInjection = function (nodeId) {
        if (!window.__drbPendingOrderInjections) { return; }
        if (window.__drbPendingOrderInjections[nodeId]) {
            delete window.__drbPendingOrderInjections[nodeId];
        }
    };

    window.__drbScheduleCapturedFetchXmlPopulation = function (nodeId, payload, attempt) {
        try {
            if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload) || !DRB.Utilities.HasValue(payload.fetchXml)) { return; }
            var currentNode = DRB.Metadata.CurrentNode;
            if (!DRB.Utilities.HasValue(currentNode) || currentNode.id !== nodeId) {
                window.__drbClearPendingFetchInjection(nodeId);
                return;
            }
            if (DRB.Utilities.HasValue(currentNode.data.__capturedFetchApplied)) {
                window.__drbClearPendingFetchInjection(nodeId);
                return;
            }

            var currentAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
            if (!DRB.Utilities.HasValue(payload.__drbFetchPopulateStart)) {
                payload.__drbFetchPopulateStart = Date.now();
            }
            var maxWait = 120000;

            if (!window.__drbCanPopulateCapturedFetchXml()) {
                window.__drbTrackPendingFetchInjection(nodeId, payload, currentAttempt);
                var elapsed = Date.now() - payload.__drbFetchPopulateStart;
                if (elapsed > maxWait) {
                    window.__drbClearPendingFetchInjection(nodeId);
                    delete payload.__drbFetchPopulateStart;
                    return;
                }
                var backoff = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedFetchXmlPopulation(nodeId, payload, currentAttempt + 1); }, backoff);
                return;
            }

            var applied = window.__drbApplyCapturedFetchXml(payload);
            if (applied !== true) {
                window.__drbTrackPendingFetchInjection(nodeId, payload, currentAttempt);
                var attemptElapsed = Date.now() - payload.__drbFetchPopulateStart;
                if (attemptElapsed > maxWait) {
                    window.__drbClearPendingFetchInjection(nodeId);
                    delete payload.__drbFetchPopulateStart;
                    return;
                }
                var retryDelay = Math.min(500 + (currentAttempt * 50), 2000);
                setTimeout(function () { window.__drbScheduleCapturedFetchXmlPopulation(nodeId, payload, currentAttempt + 1); }, retryDelay);
                return;
            }

            window.__drbClearPendingFetchInjection(nodeId);
            delete payload.__drbFetchPopulateStart;
            currentNode.data.__capturedFetchApplied = true;
        } catch (fetchError) {
            console.error(fetchError);
        }
    };

    window.__drbCanPopulateCapturedFetchXml = function () {
        if (!DRB.Utilities.HasValue(DRB.Metadata.CurrentNode)) { return false; }
        var data = DRB.Metadata.CurrentNode.data;
        if (!DRB.Utilities.HasValue(data) || data.requestType !== "predefinedquery") { return false; }
        var queryTypeDropdown = $("#" + DRB.DOM.QueryType.Dropdown.Id);
        if (queryTypeDropdown.length === 0) { return false; }
        if (!DRB.Utilities.HasValue(DRB.Metadata.XMLEditor) || !DRB.Utilities.HasValue(DRB.Metadata.XMLEditor.session)) { return false; }
        return true;
    };

    window.__drbFormatFetchXml = function (rawXml) {
        if (!DRB.Utilities.HasValue(rawXml) || typeof rawXml !== "string") { return rawXml; }
        var xmlContent = rawXml.trim();
        if (xmlContent.length === 0) { return rawXml; }
        try {
            if (typeof DOMParser !== "undefined") {
                var parser = new DOMParser();
                var parsed = parser.parseFromString(xmlContent, "application/xml");
                var parseErrors = parsed.getElementsByTagName("parsererror");
                if (!parseErrors || parseErrors.length === 0) {
                    var formattedDom = window.__drbFormatXmlElement(parsed.documentElement, 0);
                    if (Array.isArray(formattedDom) && formattedDom.length > 0) {
                        return formattedDom.join("\n");
                    }
                }
            }
        } catch { }

        return window.__drbFallbackFormatXml(xmlContent);
    };

    window.__drbFormatXmlElement = function (element, level) {
        if (!DRB.Utilities.HasValue(element) || element.nodeType !== 1) { return []; }
        var indentUnit = "    ";
        var padding = new Array(level + 1).join(indentUnit);
        var opening = "<" + element.nodeName;
        if (element.attributes && element.attributes.length > 0) {
            var attributeParts = [];
            for (var attrIndex = 0; attrIndex < element.attributes.length; attrIndex++) {
                var attribute = element.attributes[attrIndex];
                attributeParts.push(attribute.name + '="' + attribute.value + '"');
            }
            opening += " " + attributeParts.join(" ");
        }

        var hasElementChildren = false;
        var hasTextContent = false;
        for (var child = element.firstChild; child; child = child.nextSibling) {
            if (child.nodeType === 1) { hasElementChildren = true; }
            if (child.nodeType === 3 && child.nodeValue.trim().length > 0) { hasTextContent = true; }
        }

        var lines = [];
        if (!hasElementChildren && hasTextContent === false) {
            lines.push(padding + opening + " />");
            return lines;
        }

        lines.push(padding + opening + ">");
        for (var current = element.firstChild; current; current = current.nextSibling) {
            if (current.nodeType === 1) {
                lines = lines.concat(window.__drbFormatXmlElement(current, level + 1));
            } else if (current.nodeType === 3) {
                var textValue = current.nodeValue.trim();
                if (textValue.length > 0) {
                    var textPadding = new Array(level + 2).join(indentUnit);
                    lines.push(textPadding + textValue);
                }
            }
        }
        lines.push(padding + "</" + element.nodeName + ">");
        return lines;
    };

    window.__drbFallbackFormatXml = function (xmlContent) {
        var formatted = [];
        var indent = 0;
        var newlineSeparated = xmlContent.replace(/>\s*</g, '>' + "\n" + '<');
        var lines = newlineSeparated.split("\n");
        lines.forEach(function (line) {
            var trimmedLine = line.trim();
            if (trimmedLine.length === 0) { return; }
            if (/^<\//.test(trimmedLine)) { indent = Math.max(indent - 1, 0); }
            var padding = new Array(indent + 1).join("    ");
            formatted.push(padding + trimmedLine);
            if (/^<[^!?\/][^>]*[^\/]?>$/.test(trimmedLine)) { indent += 1; }
        });
        return formatted.join("\n");
    };

    window.__drbApplyCapturedFetchXml = function (payload) {
        if (!DRB.Utilities.HasValue(payload) || !DRB.Utilities.HasValue(payload.fetchXml)) { return false; }
        var desiredQueryType = DRB.Utilities.HasValue(payload.queryType) ? payload.queryType : "fetchxml";
        var queryTypeDropdown = $("#" + DRB.DOM.QueryType.Dropdown.Id);
        if (queryTypeDropdown.length === 0) { return false; }
        if (queryTypeDropdown.val() !== desiredQueryType) {
            queryTypeDropdown.val(desiredQueryType).change();
        } else {
            queryTypeDropdown.trigger("change");
        }

        if (!DRB.Utilities.HasValue(DRB.Metadata.XMLEditor) || !DRB.Utilities.HasValue(DRB.Metadata.XMLEditor.session)) { return false; }
        var formattedFetchXml = window.__drbFormatFetchXml(payload.fetchXml);
        DRB.Metadata.XMLEditor.session.setValue(formattedFetchXml);

        if (DRB.Utilities.HasValue(DRB.Metadata.CurrentNode) && DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data) && DRB.Utilities.HasValue(DRB.Metadata.CurrentNode.data.configuration)) {
            DRB.Metadata.CurrentNode.data.configuration.queryType = desiredQueryType;
            DRB.Metadata.CurrentNode.data.configuration.fetchXML = formattedFetchXml;
        }

        return true;
    };

    window.__drbTrackPendingFetchInjection = function (nodeId, payload, attempt) {
        if (!DRB.Utilities.HasValue(nodeId) || !DRB.Utilities.HasValue(payload)) { return; }
        if (!window.__drbPendingFetchXmlInjections) { window.__drbPendingFetchXmlInjections = {}; }
        var cleanAttempt = DRB.Utilities.HasValue(attempt) ? attempt : 0;
        window.__drbPendingFetchXmlInjections[nodeId] = { payload: payload, attempt: cleanAttempt };
    };

    window.__drbClearPendingFetchInjection = function (nodeId) {
        if (!window.__drbPendingFetchXmlInjections) { return; }
        if (window.__drbPendingFetchXmlInjections[nodeId]) {
            delete window.__drbPendingFetchXmlInjections[nodeId];
        }
    };

    window.__drbNotifyCapturedFetchXmlReady = function () {
        if (!window.__drbPendingFetchXmlInjections) { return; }
        Object.keys(window.__drbPendingFetchXmlInjections).forEach(function (nodeId) {
            var entry = window.__drbPendingFetchXmlInjections[nodeId];
            if (!entry || !entry.payload) { return; }
            window.__drbScheduleCapturedFetchXmlPopulation(nodeId, entry.payload, (entry.attempt || 0) + 1);
        });
    };

    window.__drbFindColumnByLogicalName = function (logicalName) {
        if (!DRB.Utilities.HasValue(logicalName) || !Array.isArray(DRB.Metadata.CurrentColumns)) { return null; }
        var comparer = logicalName.toLowerCase();
        for (var index = 0; index < DRB.Metadata.CurrentColumns.length; index++) {
            var column = DRB.Metadata.CurrentColumns[index];
            if (!DRB.Utilities.HasValue(column) || !DRB.Utilities.HasValue(column.LogicalName)) { continue; }
            if (column.LogicalName.toLowerCase() === comparer) { return column; }
        }
        return null;
    };

    window.__drbFindQueryValue = function (payload, key) {
        if (!DRB.Utilities.HasValue(payload) || !DRB.Utilities.HasValue(key)) { return null; }
        var query = payload.query || payload.Query;
        if (DRB.Utilities.HasValue(query)) {
            var found = window.__drbFindValueIgnoreCase(query, key);
            if (DRB.Utilities.HasValue(found)) { return found; }
        }

        var rawUrl = payload.url || payload.originalUrl || payload.Url || payload.OriginalUrl;
        if (!DRB.Utilities.HasValue(rawUrl)) { return null; }
        try {
            var parsed = null;
            try {
                parsed = new URL(rawUrl);
            } catch {
                parsed = new URL(rawUrl, "https://placeholder");
            }
            if (parsed && parsed.searchParams) {
                var value = parsed.searchParams.get(key);
                if (DRB.Utilities.HasValue(value)) { return value; }
            }
        } catch { }
        return null;
    };

    window.__drbFindValueIgnoreCase = function (obj, key) {
        if (!DRB.Utilities.HasValue(obj) || typeof obj !== "object") { return null; }
        var target = key.toLowerCase();
        var value = null;
        Object.keys(obj).forEach(function (currentKey) {
            if (currentKey.toLowerCase() === target && !DRB.Utilities.HasValue(value)) {
                value = obj[currentKey];
            }
        });
        return value;
    };
}

(function () {
    if (typeof chrome === "undefined" || !DRB.Utilities.HasValue(chrome.webview) || typeof chrome.webview.addEventListener !== "function") { return; }
    chrome.webview.addEventListener("message", function (event) {
        try {
            var data = event && event.data ? event.data : null;
            if (typeof data === "string") { data = JSON.parse(data); }
            if (!DRB.Utilities.HasValue(data) || !data.action) { return; }
            if (data.action === "captured-request") {
                if (typeof window.__drbReceiveCapturedRequest === "function") {
                    window.__drbReceiveCapturedRequest(data.data);
                }
            } else if (data.action === "restmetadata-get-result" || data.action === "restmetadata-set-result") {
                if (DRB.Utilities.HasValue(DRB.Xrm) && typeof DRB.Xrm.HandleWebViewMessage === "function") {
                    DRB.Xrm.HandleWebViewMessage(data);
                }
            }
        } catch (ex) {
            console.error(ex);
        }
    });
})();
DRB.InsertMainBodyContent = function () {
        $("#" + DRB.DOM.MainBody.Id).html(`
        <div class="drb-shell">
            <header class="drb-header">
                <div class="drb-header__titles">
                    <div class="drb-header__title-row">
                        <h1 class="drb-header__title">REST Builder</h1>
                        <span id="${DRB.DOM.ContextSpan.Id}" class="drb-context-pill">Detecting context...</span>
                    </div>
                    <p class="drb-header__subtitle">Compose, organize, and execute Dataverse calls.</p>
                </div>
                <div class="drb-header__actions">
                    <div class="drb-header__badge">
                        <span class="drb-header__dot"></span>
                        Live workspace
                    </div>
                </div>
            </header>
            <div class="drb-body split">
                <aside id="${DRB.DOM.Split.Menu.Id}" class="drb-panel drb-panel--left">
                    <div class="drb-card drb-card--tree">
                        <div class="drb-card__title-row">
                            <div>
                                <p class="drb-eyebrow">Collections</p>
                                <h2 class="drb-card__title">Request Explorer</h2>
                            </div>
                        </div>
                        <div class="drb-tree-actions" id="${DRB.DOM.Collection.Menu.Id}" role="group" aria-label="Collection actions"></div>
                        <div class="drb-card__body">
                            <div id="${DRB.DOM.TreeView.Id}" class="drb-tree"></div>
                        </div>
                    </div>
                </aside>
                <section id="${DRB.DOM.Split.Content.Id}" class="drb-panel drb-panel--right">
                    <div class="drb-card drb-card--content">
                        <div id="${DRB.DOM.MainContent.Id}" class="drb-main-content" style="display: none;"></div>
                    </div>
                </section>
            </div>
        </div>`);
}

/**
 * Main function called by the Index
 */
DRB.Initialize = async function () {
    // localStorage
    DRB.Settings.LocalStorageAvailable = DRB.Utilities.LocalStorageAvailable();

    // #region XTB
    DRB.Settings.XTBContext = false;
    var xtbSettings = null;
    try {
        if (DRB.Utilities.HasValue(chrome) && DRB.Utilities.HasValue(chrome.webview) && DRB.Utilities.HasValue(chrome.webview.hostObjects)) {
            xtbSettings = chrome.webview.hostObjects.xtbSettings;
        }
    } catch { }

    if (DRB.Utilities.HasValue(xtbSettings)) {
        DRB.Settings.XTBToken = await xtbSettings.Token;
        DRB.Settings.XTBUrl = await xtbSettings.Url;
        DRB.Settings.XTBVersion = await xtbSettings.Version;
        if (DRB.Utilities.HasValue(DRB.Settings.XTBToken) && DRB.Utilities.HasValue(DRB.Settings.XTBUrl) && DRB.Utilities.HasValue(DRB.Settings.XTBVersion)) {
            DRB.Settings.XTBUrl = DRB.Settings.XTBUrl.replace(/\/$/, ""); // clean url from trailing slash
            DRB.Settings.XTBContext = true;
        }
        try {
            var hostIsDark = await xtbSettings.IsDarkMode;
            DRB.Theme.apply(hostIsDark === false ? "light" : "dark");
        } catch (themeError) { }
    }
    // #endregion

    // #region JWT
    DRB.Settings.JWTContext = false;
    if (DRB.Xrm.IsXTBMode() === false && DRB.Settings.LocalStorageAvailable === true) {
        try {
            if (localStorage.getItem("DRB_JWT") !== null) {
                var removeToken = true;
                var token = localStorage.getItem("DRB_JWT");
                var parsedToken = DRB.Common.ParseJWT(token);
                if (DRB.Utilities.HasValue(parsedToken)) {
                    var jwtUrl = parsedToken.aud;
                    var jwtExpireDate = parsedToken.exp * 1000;
                    var now = new Date().getTime();
                    if (DRB.Utilities.HasValue(jwtUrl) && jwtExpireDate > now) {
                        jwtUrl = jwtUrl.replace(/\/$/, ""); // clean url from trailing slash
                        DRB.UI.ShowLoading("Checking JWT Settings...");
                        try {
                            await DRB.Xrm.GetServerVersion(jwtUrl, token).done(function (data) {
                                DRB.Settings.JWTToken = token;
                                DRB.Settings.JWTUrl = jwtUrl;
                                DRB.Settings.JWTVersion = data.Version;
                                DRB.Settings.JWTContext = true;
                                removeToken = false;
                            });
                        } catch { }
                        DRB.UI.HideLoading();
                    }
                }
                if (removeToken === true) { localStorage.removeItem("DRB_JWT"); }
            }
        } catch {
            // something went wrong, remove the token
            localStorage.removeItem("DRB_JWT");
        }
    }
    // #endregion

    // #region BE
    DRB.Settings.BEContext = false;
    // #endregion

    // #region DVDT
    DRB.Settings.DVDTContext = false;
    // #endregion

    Split(["#" + DRB.DOM.Split.Menu.Id, "#" + DRB.DOM.Split.Content.Id], { sizes: [10, 90], minSize: 200, gutterSize: 5 }); // Split
    DRB.SetDefaultSettings();
    DRB.DefineOperations();
    // Ensure a default collection exists on first load.
    try {
        var tree = $("#" + DRB.DOM.TreeView.Id).jstree(true);
        if (DRB.Utilities.HasValue(tree) && tree.get_json().length === 0) { DRB.Collection.CreateDefault(); }
    } catch (ex) { }

    // Tab script
    $(document).ready(function () {
        $("#" + DRB.DOM.TabsRequest.Id + " a").click(function (e) {
            e.preventDefault();
            if (e.target.id.length > 2 && e.target.id.indexOf("a_") === 0) {
                var tabName = e.target.id.substring(2);
                var checkTab = DRB.Utilities.GetRecordById(DRB.Settings.Tabs, tabName);
                if (DRB.Utilities.HasValue(checkTab) && checkTab.GenerateCode === true) {
                    DRB.GenerateCode.Start();
                }
            }
            $(this).tab('show');
        });
    });

    // Complete Initialize
    DRB.Logic.CompleteInitialize();
}
// #endregion


