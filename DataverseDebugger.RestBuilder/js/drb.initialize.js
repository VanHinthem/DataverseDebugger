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
    var btn_executeRequest = DRB.UI.CreateButton("btn_execute_request", "Execute", "btn-danger", function () {
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
if (typeof window !== "undefined" && typeof window.__drbReceiveCapturedRequest !== "function") {
    window.__drbReceiveCapturedRequest = function (payload) {
        if (!window.DRB) { return false; }
        if (!DRB.Injection) { DRB.Injection = {}; }

        if (!Array.isArray(DRB.Injection.queue)) {
            DRB.Injection.queue = [];
        }

        if (!DRB.Injection.applyRequest) {
            DRB.Injection.applyRequest = function (request) {
                if (!request) { return false; }
                if (!DRB.DOM || !DRB.DOM.TreeView) { return false; }
                var tree = $("#" + DRB.DOM.TreeView.Id).jstree(true);
                if (!tree) { return false; }

                var roots = tree.get_node("#").children;
                if (!roots || roots.length === 0) {
                    DRB.Collection.CreateDefault();
                    roots = tree.get_node("#").children;
                }

                var parent = roots.length > 0 ? tree.get_node(roots[0]) : tree.get_node("#");
                var name = request.requestName || "New Request";
                var nodeId = tree.create_node(parent, { type: "request", text: name }, "last");
                var node = tree.get_node(nodeId);
                if (!node) { return false; }

                if (!node.data) { node.data = { endpoint: "webapi", requestType: "", configuration: {} }; }
                if (!node.data.configuration) { node.data.configuration = {}; }

                node.data.endpoint = "webapi";
                node.data.requestType = (request.requestType || "").toLowerCase();

                var config = node.data.configuration;
                if (request.primaryId) { config.primaryId = request.primaryId; }

                var table = null;
                if (request.primaryEntityLogicalName && DRB.Metadata && Array.isArray(DRB.Metadata.Tables)) {
                    table = DRB.Utilities.GetRecordById(DRB.Metadata.Tables, request.primaryEntityLogicalName);
                }
                if (!table && request.entitySetName && DRB.Metadata && Array.isArray(DRB.Metadata.Tables)) {
                    for (var i = 0; i < DRB.Metadata.Tables.length; i++) {
                        if (DRB.Metadata.Tables[i].EntitySetName === request.entitySetName) {
                            table = DRB.Metadata.Tables[i];
                            break;
                        }
                    }
                }
                if (table) {
                    config.primaryEntity = { logicalName: table.LogicalName, schemaName: table.SchemaName, label: table.Name, entitySetName: table.EntitySetName };
                } else if (request.primaryEntityLogicalName || request.entitySetName) {
                    config.primaryEntity = {
                        logicalName: request.primaryEntityLogicalName || "",
                        schemaName: "",
                        label: request.primaryEntityLogicalName || request.entitySetName || "",
                        entitySetName: request.entitySetName || ""
                    };
                }

                if (request.queryType) { config.queryType = request.queryType; }
                if (request.fetchXml) { config.fetchXML = request.fetchXml; }

                tree.open_node(parent);
                tree.deselect_all();
                tree.select_node(node);
                return true;
            };
        }

        if (!DRB.Injection.isReady) {
            DRB.Injection.isReady = function () {
                return DRB.Metadata && Array.isArray(DRB.Metadata.Tables) && DRB.Metadata.Tables.length > 0;
            };
        }

        if (!DRB.Injection.flushQueue) {
            DRB.Injection.flushQueue = function () {
                if (!DRB.Injection.isReady()) { return false; }
                while (DRB.Injection.queue.length > 0) {
                    var next = DRB.Injection.queue.shift();
                    DRB.Injection.applyRequest(next);
                }
                return true;
            };
        }

        if (!DRB.Injection.scheduleFlush) {
            DRB.Injection.scheduleFlush = function () {
                if (DRB.Injection.flushTimer) { return; }
                DRB.Injection.flushTimer = setTimeout(function () {
                    DRB.Injection.flushTimer = null;
                    if (!DRB.Injection.flushQueue()) {
                        DRB.Injection.scheduleFlush();
                    }
                }, 300);
            };
        }

        if (!DRB.Injection.isReady()) {
            DRB.Injection.queue.push(payload);
            DRB.Injection.scheduleFlush();
            return false;
        }

        return DRB.Injection.applyRequest(payload);
    };

    if (window.chrome && chrome.webview && typeof chrome.webview.addEventListener === "function") {
        chrome.webview.addEventListener("message", function (event) {
            var data = event && event.data ? event.data : null;
            if (typeof data === "string") {
                try { data = JSON.parse(data); } catch { }
            }
            if (!data || !data.action) { return; }
            if (data.action === "captured-request") {
                window.__drbReceiveCapturedRequest(data.data || {});
            }
        });
    }
}

DRB.InsertMainBodyContent = function () {
        $("#" + DRB.DOM.MainBody.Id).html(`
        <div class="drb-shell">
            <header class="drb-header">
                <div class="drb-header__titles">
                    <p class="drb-header__eyebrow">Dataverse Debugger</p>
                    <div class="drb-header__title-row">
                        <h1 class="drb-header__title">REST Builder</h1>
                        <span id="${DRB.DOM.ContextSpan.Id}" class="drb-context-pill">Detecting context...</span>
                    </div>
                    <p class="drb-header__subtitle">Compose, organize, and execute Dataverse calls without leaving the debugger workspace.</p>
                    <p class="drb-header__subtitle">RB build 2025-02-11</p>
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
