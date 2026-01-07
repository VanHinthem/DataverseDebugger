// #region DRB.Xrm
/**
 * Xrm - Get Xrm Object
 */
DRB.Xrm.GetXrmObject = function () {
    try {
        if (typeof parent !== "undefined") { return parent.Xrm; } else { return undefined; }
    } catch { return undefined; }
}

/**
 * Xrm - Is XTB Mode
 */
DRB.Xrm.IsXTBMode = function () {
    return DRB.Settings.XTBContext;
}

/**
 * Xrm - Is BE Mode
 */
DRB.Xrm.IsBEMode = function () {
    return DRB.Settings.BEContext;
}

/**
 * Xrm - Is JWT Mode
 */
DRB.Xrm.IsJWTMode = function () {
    return DRB.Settings.JWTContext;
}

/**
 * Xrm - Is DVDT Mode
 */
DRB.Xrm.IsDVDTMode = function () {
    return DRB.Settings.DVDTContext;
}

/**
 * Xrm - Is Demo Mode
 */
DRB.Xrm.IsDemoMode = function () {
    if (DRB.Xrm.IsXTBMode() || DRB.Xrm.IsBEMode() || DRB.Xrm.IsJWTMode() || DRB.Xrm.IsDVDTMode()) { return false; }
    return typeof DRB.Xrm.GetXrmObject() === "undefined";
}

/**
 * Xrm - Is Instance Mode
 */
DRB.Xrm.IsInstanceMode = function () {
    if (DRB.Xrm.IsXTBMode() || DRB.Xrm.IsBEMode() || DRB.Xrm.IsJWTMode() || DRB.Xrm.IsDVDTMode() || DRB.Xrm.IsDemoMode()) { return false; }
    return typeof DRB.Xrm.GetXrmObject() !== "undefined";
}

/**
 * Xrm - Get Client Url
 */
DRB.Xrm.GetClientUrl = function () {
    if (DRB.Xrm.IsXTBMode()) { return DRB.Settings.XTBUrl; }
    if (DRB.Xrm.IsBEMode()) { return DRB.Settings.BEUrl; }
    if (DRB.Xrm.IsJWTMode()) { return DRB.Settings.JWTUrl; }
    if (DRB.Xrm.IsDVDTMode()) { return DRB.Settings.DVDTUrl; }
    if (DRB.Xrm.IsInstanceMode()) { return DRB.Xrm.GetXrmObject().Utility.getGlobalContext().getClientUrl(); }
    if (DRB.Xrm.IsDemoMode()) { return "https://democall"; }
}

/**
 * Xrm - Get Context
 */
DRB.Xrm.GetContext = function () {
    var context = "Demo";
    if (DRB.Xrm.IsXTBMode() || DRB.Xrm.IsBEMode() || DRB.Xrm.IsJWTMode() || DRB.Xrm.IsDVDTMode() || DRB.Xrm.IsInstanceMode()) { context = DRB.Xrm.GetClientUrl(); }
    return "<small>(" + context + ")</small>";
}

/**
 * Xrm - Get Metadata Url
 */
DRB.Xrm.GetMetadataUrl = function () {
    return DRB.Xrm.GetClientUrl() + "/api/data/v9.0/$metadata";
}

/**
 * Xrm - Get Current Access Token
 */
DRB.Xrm.GetCurrentAccessToken = function () {
    var token = "";
    if (DRB.Xrm.IsXTBMode()) { token = DRB.Settings.XTBToken; }
    if (DRB.Xrm.IsBEMode()) { token = DRB.Settings.BEToken; }
    if (DRB.Xrm.IsJWTMode()) { token = DRB.Settings.JWTToken; }
    if (DRB.Xrm.IsDVDTMode()) { token = DRB.Settings.DVDTToken; }
    if (DRB.Xrm.IsDemoMode()) { token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJEUkIiLCJpYXQiOjE2NDA5OTUyMDAsImV4cCI6MTY0MDk5NTIwMCwiYXVkIjoiaHR0cHM6Ly9kZW1vY2FsbCIsInN1YiI6IkRSQiJ9.niwjJ3XiFvsJkisbrcT7P27NK9v1ZfICpw5ITHP1mHo"; }
    return token;
}

/**
 * Xrm - Get Version
 */
DRB.Xrm.GetVersion = function () {
    var currentVersion = "";
    if (DRB.Xrm.IsXTBMode()) { currentVersion = DRB.Settings.XTBVersion; }
    if (DRB.Xrm.IsBEMode()) { currentVersion = DRB.Settings.BEVersion; }
    if (DRB.Xrm.IsJWTMode()) { currentVersion = DRB.Settings.JWTVersion; }
    if (DRB.Xrm.IsDVDTMode()) { currentVersion = DRB.Settings.DVDTVersion; }
    if (DRB.Xrm.IsInstanceMode()) { currentVersion = DRB.Xrm.GetXrmObject().Utility.getGlobalContext().getVersion(); }
    if (DRB.Xrm.IsDemoMode()) { currentVersion = "9.1.0.0"; }

    if (!DRB.Utilities.HasValue(currentVersion)) { return ""; }
    var versionArray = currentVersion.split(".");
    if (versionArray.length < 2) { return ""; }
    return versionArray[0] + "." + versionArray[1];
}

/**
 * Xrm - Retrieve
 * @param {string} entitySetName Entity Set Name
 * @param {string} filters Filters
 */
DRB.Xrm.Retrieve = function (entitySetName, filters) {
    var retrieveUrl = encodeURI(DRB.Xrm.GetClientUrl() + "/api/data/v9.0/" + entitySetName + "?" + filters);

    if (!DRB.Xrm.IsDemoMode()) {
        var token = DRB.Xrm.GetCurrentAccessToken();
        return $.ajax({
            type: "GET",
            contentType: "application/json; charset=utf-8",
            datatype: "json",
            async: true,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("OData-MaxVersion", "4.0");
                xhr.setRequestHeader("OData-Version", "4.0");
                xhr.setRequestHeader("Accept", "application/json");
                xhr.setRequestHeader("Prefer", "odata.include-annotations=\"*\"");
                if (DRB.Utilities.HasValue(token)) { xhr.setRequestHeader("Authorization", "Bearer " + token); }
            },
            url: retrieveUrl
        });
    }

    if (DRB.Xrm.IsDemoMode()) { return $.when(DRB.Xrm.GetDemoData(entitySetName, filters)); }
}

/**
 * Xrm - Retrieve (cached metadata)
 * @param {string} entitySetName Entity Set Name
 * @param {string} filters Filters
 */
DRB.Xrm.RetrieveCached = function (entitySetName, filters) {
    var retrieveUrl = encodeURI(DRB.Xrm.GetClientUrl() + "/api/data/v9.0/" + entitySetName + "?" + filters);

    if (DRB.Xrm.IsDemoMode()) { return $.when(DRB.Xrm.GetDemoData(entitySetName, filters)); }

    return DRB.Xrm.ExecuteCachedRequest({
        method: "GET",
        url: retrieveUrl,
        dataType: "json",
        request: function () {
            var token = DRB.Xrm.GetCurrentAccessToken();
            return $.ajax({
                type: "GET",
                contentType: "application/json; charset=utf-8",
                datatype: "json",
                async: true,
                beforeSend: function (xhr) {
                    xhr.setRequestHeader("OData-MaxVersion", "4.0");
                    xhr.setRequestHeader("OData-Version", "4.0");
                    xhr.setRequestHeader("Accept", "application/json");
                    xhr.setRequestHeader("Prefer", "odata.include-annotations=\"*\"");
                    DRB.Xrm.ApplyMetadataHeaders(xhr);
                    if (DRB.Utilities.HasValue(token)) { xhr.setRequestHeader("Authorization", "Bearer " + token); }
                },
                url: retrieveUrl
            });
        }
    });
}

/**
 * Xrm - Retrieve Batch
 * @param {any[]} queries Queries
 */
DRB.Xrm.RetrieveBatch = function (queries) {
    var batchDescription = "batch_" + DRB.Utilities.GenerateGuid();
    var data = [];
    queries.forEach(function (query) {
        var retrieveUrl = DRB.Xrm.GetClientUrl() + "/api/data/v9.0/" + query.EntitySetName + "?" + query.Filters;
        data.push("--" + batchDescription);
        data.push("Content-Type: application/http");
        data.push("Content-Transfer-Encoding: binary");
        data.push("");
        data.push("GET " + retrieveUrl + " HTTP/1.1");
        data.push("Content-Type: application/json");
        data.push("OData-Version: 4.0");
        data.push("OData-MaxVersion: 4.0");
        data.push("Prefer: odata.include-annotations=\"*\"");
        data.push("");
    });
    data.push("--" + batchDescription + "--");
    var payload = data.join("\r\n");

    if (!DRB.Xrm.IsDemoMode()) {
        var token = DRB.Xrm.GetCurrentAccessToken();
        return $.ajax({
            method: "POST",
            data: payload,
            async: true,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Content-Type", "multipart/mixed;boundary=" + batchDescription);
                xhr.setRequestHeader("OData-MaxVersion", "4.0");
                xhr.setRequestHeader("OData-Version", "4.0");
                xhr.setRequestHeader("Accept", "application/json");
                if (DRB.Utilities.HasValue(token)) { xhr.setRequestHeader("Authorization", "Bearer " + token); }
            },
            url: DRB.Xrm.GetClientUrl() + "/api/data/v9.0/$batch"
        });
    }

    if (DRB.Xrm.IsDemoMode()) { return $.when(DRB.Xrm.GetDemoDataBatch(queries)); }
}

/**
 * Xrm - Retrieve Batch (cached metadata)
 * @param {any[]} queries Queries
 */
DRB.Xrm.RetrieveBatchCached = function (queries) {
    var batchDescription = "batch_" + DRB.Utilities.GenerateGuid();
    var data = [];
    queries.forEach(function (query) {
        var retrieveUrl = DRB.Xrm.GetClientUrl() + "/api/data/v9.0/" + query.EntitySetName + "?" + query.Filters;
        data.push("--" + batchDescription);
        data.push("Content-Type: application/http");
        data.push("Content-Transfer-Encoding: binary");
        data.push("");
        data.push("GET " + retrieveUrl + " HTTP/1.1");
        data.push("Content-Type: application/json");
        data.push("OData-Version: 4.0");
        data.push("OData-MaxVersion: 4.0");
        data.push("Prefer: odata.include-annotations=\"*\"");
        data.push("");
    });
    data.push("--" + batchDescription + "--");
    var payload = data.join("\r\n");
    var batchUrl = DRB.Xrm.GetClientUrl() + "/api/data/v9.0/$batch";
    var cacheKey = DRB.Xrm.BuildBatchCacheKey(queries);

    if (DRB.Xrm.IsDemoMode()) { return $.when(DRB.Xrm.GetDemoDataBatch(queries)); }

    return DRB.Xrm.ExecuteCachedRequest({
        method: "POST",
        url: batchUrl,
        body: payload,
        cacheKey: cacheKey,
        dataType: "text",
        request: function () {
            var token = DRB.Xrm.GetCurrentAccessToken();
            return $.ajax({
                method: "POST",
                data: payload,
                async: true,
                beforeSend: function (xhr) {
                    xhr.setRequestHeader("Content-Type", "multipart/mixed;boundary=" + batchDescription);
                    xhr.setRequestHeader("OData-MaxVersion", "4.0");
                    xhr.setRequestHeader("OData-Version", "4.0");
                    xhr.setRequestHeader("Accept", "application/json");
                    DRB.Xrm.ApplyMetadataHeaders(xhr, cacheKey);
                    if (DRB.Utilities.HasValue(token)) { xhr.setRequestHeader("Authorization", "Bearer " + token); }
                },
                url: batchUrl
            });
        }
    });
}

/**
 * Xrm - Retrieve Batches
 * @param {any[]} batchedQueries Batched Queries
 */
DRB.Xrm.RetrieveBatches = function (batchedQueries) {
    var xrmCalls = [];
    batchedQueries.forEach(function (batchedQuery) {
        var queries = [];
        batchedQuery.forEach(function (query) { queries.push(query); });
        xrmCalls.push(DRB.Xrm.RetrieveBatch(queries));
    });
    return $.when.apply($, xrmCalls);
}

/**
 * Xrm - Retrieve Batches (cached metadata)
 * @param {any[]} batchedQueries Batched Queries
 */
DRB.Xrm.RetrieveBatchesCached = function (batchedQueries) {
    var xrmCalls = [];
    batchedQueries.forEach(function (batchedQuery) {
        var queries = [];
        batchedQuery.forEach(function (query) { queries.push(query); });
        xrmCalls.push(DRB.Xrm.RetrieveBatchCached(queries));
    });
    return $.when.apply($, xrmCalls);
}

/**
 * Xrm - Retrieve Metadata
 * Get $metadata content (XML)
 */
DRB.Xrm.RetrieveMetadata = function () {
    if (!DRB.Xrm.IsDemoMode()) {
        var token = DRB.Xrm.GetCurrentAccessToken();
        return $.ajax({
            type: "GET",
            datatype: "xml",
            async: true,
            beforeSend: function (xhr) {
                if (DRB.Utilities.HasValue(token)) { xhr.setRequestHeader("Authorization", "Bearer " + token); }
            },
            url: DRB.Xrm.GetMetadataUrl()
        });
    }

    if (DRB.Xrm.IsDemoMode()) { return $.when(DRB.Xrm.GetDemoMetadata()); }
}

/**
 * Xrm - Retrieve Metadata (cached metadata)
 * Get $metadata content (XML)
 */
DRB.Xrm.RetrieveMetadataCached = function () {
    if (DRB.Xrm.IsDemoMode()) { return $.when(DRB.Xrm.GetDemoMetadata()); }

    var metadataUrl = DRB.Xrm.GetMetadataUrl();
    return DRB.Xrm.ExecuteCachedRequest({
        method: "GET",
        url: metadataUrl,
        dataType: "xml",
        request: function () {
            var token = DRB.Xrm.GetCurrentAccessToken();
            return $.ajax({
                type: "GET",
                datatype: "xml",
                async: true,
                beforeSend: function (xhr) {
                    DRB.Xrm.ApplyMetadataHeaders(xhr);
                    if (DRB.Utilities.HasValue(token)) { xhr.setRequestHeader("Authorization", "Bearer " + token); }
                },
                url: metadataUrl
            });
        }
    });
}

DRB.Xrm.IsWebViewHostAvailable = function () {
    return typeof chrome !== "undefined" &&
        DRB.Utilities.HasValue(chrome.webview) &&
        typeof chrome.webview.postMessage === "function";
}

DRB.Xrm.ExecuteCachedRequest = function (options) {
    var deferred = $.Deferred();
    if (!options || !DRB.Utilities.HasValue(options.url) || !DRB.Utilities.HasValue(options.request)) {
        deferred.reject();
        return deferred.promise();
    }

    var method = options.method || "GET";
    var url = options.url;
    var body = options.body || null;
    var dataType = options.dataType || "text";
    var cacheKey = options.cacheKey || null;

    var ajax = options.request();
    ajax.done(function (data, textStatus, jqXHR) {
        var responseText = "";
        var statusCode = 200;
        var contentType = "";
        try {
            if (DRB.Utilities.HasValue(jqXHR)) {
                statusCode = jqXHR.status || statusCode;
                responseText = jqXHR.responseText || "";
                if (typeof jqXHR.getResponseHeader === "function") {
                    contentType = jqXHR.getResponseHeader("Content-Type") || "";
                }
            }
        } catch { }

        if (DRB.Utilities.HasValue(responseText)) {
            DRB.Xrm.StoreRestMetadataCache(method, url, body, responseText, statusCode, cacheKey, contentType);
        }

        deferred.resolve(data);
    })
        .fail(function (xhr) {
            deferred.reject(xhr);
        });

    return deferred.promise();
}

DRB.Xrm.ParseCachedResponse = function (body, dataType) {
    if (!DRB.Utilities.HasValue(body)) { return body; }
    if (dataType === "xml") {
        try { return $.parseXML(body); } catch { return body; }
    }
    if (dataType === "json") {
        try { return JSON.parse(body); } catch { return body; }
    }
    return body;
}

DRB.Xrm.RequestRestMetadataCache = function (method, url, body, cacheKey) {
    var deferred = $.Deferred();
    if (!DRB.Xrm.IsWebViewHostAvailable()) {
        deferred.resolve({ hit: false });
        return deferred.promise();
    }

    var requestId = DRB.Utilities.GenerateGuid();
    if (!DRB.Xrm._webViewRequests) { DRB.Xrm._webViewRequests = {}; }
    DRB.Xrm._webViewRequests[requestId] = deferred;

    var payload = { method: method, url: url };
    if (DRB.Utilities.HasValue(body)) { payload.body = body; }
    if (DRB.Utilities.HasValue(cacheKey)) { payload.cacheKey = cacheKey; }

    var message = { action: "restmetadata-get", requestId: requestId, data: payload };
    try { chrome.webview.postMessage(message); } catch { deferred.resolve({ hit: false }); }
    return deferred.promise();
}

DRB.Xrm.StoreRestMetadataCache = function (method, url, body, responseText, statusCode, cacheKey, contentType) {
    if (!DRB.Xrm.IsWebViewHostAvailable() || !DRB.Utilities.HasValue(responseText)) { return; }
    var payload = {
        method: method,
        url: url,
        statusCode: statusCode || 200,
        responseText: responseText,
        responseEncoding: "utf-8"
    };
    if (DRB.Utilities.HasValue(body)) { payload.body = body; }
    if (DRB.Utilities.HasValue(cacheKey)) { payload.cacheKey = cacheKey; }
    if (DRB.Utilities.HasValue(contentType)) { payload.contentType = contentType; }

    var message = { action: "restmetadata-set", data: payload };
    try { chrome.webview.postMessage(message); } catch { }
}

DRB.Xrm.HandleWebViewMessage = function (message) {
    if (!message || !message.requestId) { return; }
    if (!DRB.Xrm._webViewRequests) { return; }
    var deferred = DRB.Xrm._webViewRequests[message.requestId];
    if (!deferred) { return; }
    delete DRB.Xrm._webViewRequests[message.requestId];
    deferred.resolve(message);
}

DRB.Xrm.BuildBatchCacheKey = function (queries) {
    if (!Array.isArray(queries) || queries.length === 0) { return ""; }
    var parts = [];
    queries.forEach(function (query) {
        if (!DRB.Utilities.HasValue(query)) { return; }
        var name = query.EntitySetName || "";
        var filters = query.Filters || "";
        parts.push(name + "?" + filters);
    });
    var joined = parts.join("||");
    return DRB.Xrm.HashString(joined);
}

DRB.Xrm.HashString = function (value) {
    if (!DRB.Utilities.HasValue(value)) { return ""; }
    var hash = 0xcbf29ce484222325n;
    for (var i = 0; i < value.length; i++) {
        hash ^= BigInt(value.charCodeAt(i));
        hash = (hash * 0x100000001b3n) & 0xffffffffffffffffn;
    }
    var hex = hash.toString(16);
    while (hex.length < 16) { hex = "0" + hex; }
    return hex;
}

DRB.Xrm.ApplyMetadataHeaders = function (xhr, cacheKey) {
    if (!DRB.Utilities.HasValue(xhr)) { return; }
    try {
        xhr.setRequestHeader("x-drb-metadata", "1");
        if (DRB.Utilities.HasValue(cacheKey)) {
            xhr.setRequestHeader("x-drb-cachekey", cacheKey);
        }
    } catch { }
}

/**
 * Xrm - Get Server Version
 * @param {string} serverUrl Server Url
 * @param {string} token Token
 */
DRB.Xrm.GetServerVersion = function (serverUrl, token) {
    return $.ajax({
        type: "GET",
        contentType: "application/json; charset=utf-8",
        datatype: "json",
        async: true,
        beforeSend: function (xhr) {
            xhr.setRequestHeader("OData-MaxVersion", "4.0");
            xhr.setRequestHeader("OData-Version", "4.0");
            xhr.setRequestHeader("Accept", "application/json");
            xhr.setRequestHeader("Authorization", "Bearer " + token);
        },
        url: serverUrl + "/api/data/v9.0/RetrieveVersion()"
    });
}
// #endregion
