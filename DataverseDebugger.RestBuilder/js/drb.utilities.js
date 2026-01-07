// #region DRB.Utilities
/**
 * Utilities - Has Value
 * Returns true if a parameter is not undefined, not null and not an empty string, otherwise returns false
 * @param {any} parameter Parameter to check
 */
DRB.Utilities.HasValue = function (parameter) {
    if (parameter !== undefined && parameter !== null && parameter !== "") { return true; } else { return false; }
}

/**
 * Utilities - Format XML
 * Returns a pretty printed XML string when possible
 * @param {string} xml XML string
 */
DRB.Utilities.FormatXml = function (xml) {
    if (!DRB.Utilities.HasValue(xml)) { return ""; }
    var trimmed = ("" + xml).trim();
    if (trimmed === "") { return ""; }

    try {
        if (typeof DOMParser === "undefined" || typeof XMLSerializer === "undefined") { return trimmed; }
        var parser = new DOMParser();
        var doc = parser.parseFromString(trimmed, "application/xml");
        var errors = doc.getElementsByTagName("parsererror");
        if (errors && errors.length > 0) { return trimmed; }

        var serialized = new XMLSerializer().serializeToString(doc);
        var tokens = serialized.replace(/(>)(<)(\/*)/g, "$1\n$2$3").split("\n");
        var indent = 0;
        var formatted = [];

        tokens.forEach(function (token) {
            var line = token.trim();
            if (line === "") { return; }
            if (line.match(/^<\//)) { indent = Math.max(indent - 1, 0); }
            var padding = new Array(indent + 1).join("  ");
            formatted.push(padding + line);
            if (line.match(/^<[^!?/][^>]*[^/]>$/)) { indent += 1; }
        });

        return formatted.join("\n");
    } catch (e) {
        return trimmed;
    }
}

/**
 * Utilities - Download File
 * Download a file (added for BE mode)
 */
DRB.Utilities.DownloadFile = function (blob, fileName) {
    try {
        if (!DRB.Xrm.IsBEMode()) {
            var customLink = document.createElement("a");
            customLink.href = URL.createObjectURL(blob);
            customLink.download = fileName;
            customLink.click();
        } else {
            parent.postMessage({ command: "be_downloadfile", blob: blob, fileName: fileName }, '*');
        }
    } catch (e) { }
}


/**
 * Utilities - Local Storage Available
 * Check if localStorage is available
 */
DRB.Utilities.LocalStorageAvailable = function () {
    try {
        localStorage.setItem("DRB_CheckLocalStorage", "DRB");
        localStorage.removeItem("DRB_CheckLocalStorage");
        return true;
    } catch (e) { return false; }
}

/**
 * Utilities - Generate Guid
 * Returns a Random Guid with options to add Braces or Upper Case
 * @param {boolean} braces if the Guid contains braces
 * @param {boolean} upperCase if the Guid is returned as Upper Case
 */
DRB.Utilities.GenerateGuid = function (braces, upperCase) {
    var randomGuid = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".replace(/x/g, function (c) {
        var r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
    if (braces === true) { randomGuid = "{" + randomGuid + "}"; }
    if (upperCase === true) { randomGuid = randomGuid.toUpperCase(); }
    return randomGuid;
}

/**
 * Utilities - Remove Duplicates From Array
 * Returns a new Array without duplicates
 * @param {any[]} array Array to check
 */
DRB.Utilities.RemoveDuplicatesFromArray = function (array) {
    var values = array.filter(function (item, pos) { return array.indexOf(item) === pos; });
    return values;
}

/**
 * Utilities - Get Record By Property
 * Returns a Record matching the property and the value passed
 * @param {any[]} records Records
 * @param {string} propertyName Property Name
 * @param {any} propertyValue Property Value
 */
DRB.Utilities.GetRecordByProperty = function (records, propertyName, propertyValue) {
    if (Array.isArray(records)) {
        for (var count = 0; count < records.length; count++) {
            if (records[count].hasOwnProperty(propertyName) && records[count][propertyName] == propertyValue) { return records[count]; }
        }
    }
    return null;
}

/**
 * Utilities - Get Record By Id
 * Returns a record matching the id
 * @param {any[]} records Records
 * @param {any} id Id
 */
DRB.Utilities.GetRecordById = function (records, id) {
    return DRB.Utilities.GetRecordByProperty(records, "Id", id);
}

/**
 * Utilities - Custom Sort
 * sort an array on a specific property, minus sign (-) in front of the property defines a reverse sort
 * @param {string} property Property Name
 */
DRB.Utilities.CustomSort = function (property) {
    var sortOrder = 1;
    if (property[0] === "-") { sortOrder = -1; property = property.substr(1); }

    return function (a, b) {
        var result = (a[property].toLowerCase() < b[property].toLowerCase()) ? -1 : (a[property].toLowerCase() > b[property].toLowerCase()) ? 1 : 0;
        return result * sortOrder;
    }
}
// #endregion
