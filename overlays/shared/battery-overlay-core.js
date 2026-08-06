/*
 * Vader Battery Tray - shared overlay core.
 *
 * Single source of truth for every non-Rainmeter overlay (OBS Browser
 * Source, Wallpaper Engine widget, and any future consumer) that reads
 * http://127.0.0.1:42115/api/v1/state. Mirrors the status text, band
 * colors, bar-fill math, and charging-accent rule already implemented in
 * rainmeter/RainformerHWi/Controller/Controller.lua, so every consumer
 * agrees on wording and color for the same live state.
 *
 * This file is not loaded directly by any overlay. Edit it, then run
 * overlays/shared/build.ps1 to splice it into overlays/obs/*.html and
 * overlays/wallpaper-engine/index.html between the
 * "BEGIN/END GENERATED CORE" marker comments. Each overlay stays a single
 * self-contained file so it can be distributed/copied independently.
 */
(function (global) {
    "use strict";

    var API_URL = "http://127.0.0.1:42115/api/v1/state";
    var POLL_INTERVAL_MS = 1000;

    var BAND_COLORS = {
        1: "#EC4040", // Critical
        2: "#FF8C00", // Low
        3: "#F5C82A", // Medium
        4: "#5BF880", // High
        5: "#3399FF"  // Top
    };
    var INACTIVE_COLOR = "#8A8A8A";

    var CHARGING_ACCENT_RED = "#EC4040";
    var CHARGING_ACCENT_YELLOW = "#F5C82A";
    var CHARGING_ACCENT_BLUE = "#3399FF";

    function chargingAccentColor(bandLevel) {
        if (bandLevel <= 2) {
            return CHARGING_ACCENT_RED;
        }
        if (bandLevel <= 4) {
            return CHARGING_ACCENT_YELLOW;
        }
        return CHARGING_ACCENT_BLUE;
    }

    function bandColor(bandLevel) {
        return BAND_COLORS[bandLevel] || INACTIVE_COLOR;
    }

    function barPercent(bandLevel) {
        var level = Math.max(0, Math.min(5, bandLevel || 0));
        return level * 20;
    }

    // Derives a plain view-model object from a parsed /api/v1/state
    // response (or null on fetch failure). Contains no DOM references so
    // it can be reused by overlays with completely different layouts.
    function deriveViewModel(snapshot, fetchFailed) {
        if (fetchFailed || !snapshot) {
            return {
                offline: true,
                label: "BRIDGE OFFLINE",
                subText: "Start Vader Battery Tray",
                bandText: "--",
                barPercent: 0,
                barColor: INACTIVE_COLOR,
                labelColor: INACTIVE_COLOR,
                connectionText: "",
                sourceText: "",
                powerText: "",
                signature: "offline"
            };
        }

        var status = snapshot.status;
        var bandLevel = snapshot.bandLevel || 0;
        var isOk = status === "ok";
        var plainColor = isOk ? bandColor(bandLevel) : INACTIVE_COLOR;
        var label;
        var subText = "";

        if (status === "starting") {
            label = "STARTING";
            subText = "Waiting for first reading";
        } else if (status === "receiver-disconnected") {
            label = "RECEIVER DISCONNECTED";
            subText = "Reconnect receiver";
        } else if (status === "controller-unavailable") {
            label = "CONTROLLER ASLEEP / OFF";
            subText = "Wake with Guide";
        } else if (!isOk) {
            label = "BATTERY UNAVAILABLE";
        } else if (snapshot.dockControllerState === "charge-sleep") {
            label = "DOCK SLEEP";
        } else if (snapshot.charging) {
            label = "CHARGING";
        } else if (snapshot.power === "Charged") {
            label = "CHARGED";
        } else {
            label = "BATTERY";
        }

        // The charging accent overrides only the status-text color, never
        // the bar fill, and only while actively charging in the Dock -
        // matching Controller.lua's statusColor rule exactly.
        var labelColor = plainColor;
        if (isOk && snapshot.charging && snapshot.connection === "Dock") {
            labelColor = chargingAccentColor(bandLevel);
        }

        var bandText = isOk && snapshot.band
            ? String(snapshot.band).toUpperCase()
            : "--";
        var sourceLabel = snapshot.source === "DockEfBand" ? "Dock" : "Controller";

        return {
            offline: false,
            label: label,
            subText: subText,
            bandText: bandText,
            barPercent: isOk ? barPercent(bandLevel) : 0,
            barColor: plainColor,
            labelColor: labelColor,
            connectionText: isOk && snapshot.connection ? snapshot.connection : "",
            sourceText: isOk ? sourceLabel : "",
            powerText: isOk && snapshot.power ? snapshot.power : "",
            signature: [
                status,
                bandLevel,
                snapshot.band,
                snapshot.charging,
                snapshot.dockControllerState,
                snapshot.power,
                snapshot.connection
            ].join("|")
        };
    }

    // Polls the state endpoint at POLL_INTERVAL_MS and invokes onChange
    // only when the derived view model's signature differs from the last
    // one shown, mirroring Controller.lua's redraw-on-change gate. Returns
    // the interval handle so a caller can stop() it if ever needed.
    function startPolling(onChange) {
        var lastSignature = null;

        function apply(viewModel) {
            if (viewModel.signature === lastSignature) {
                return;
            }
            lastSignature = viewModel.signature;
            onChange(viewModel);
        }

        function poll() {
            fetch(API_URL, { cache: "no-store" })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error("HTTP " + response.status);
                    }
                    return response.json();
                })
                .then(function (snapshot) {
                    apply(deriveViewModel(snapshot, false));
                })
                .catch(function () {
                    apply(deriveViewModel(null, true));
                });
        }

        poll();
        return global.setInterval(poll, POLL_INTERVAL_MS);
    }

    global.VaderBatteryOverlayCore = {
        API_URL: API_URL,
        POLL_INTERVAL_MS: POLL_INTERVAL_MS,
        BAND_COLORS: BAND_COLORS,
        INACTIVE_COLOR: INACTIVE_COLOR,
        deriveViewModel: deriveViewModel,
        startPolling: startPolling
    };
})(window);
