local currentLevel = 0
local lastSignature = nil

local function jsonString(json, key)
    return json:match('"' .. key .. '":"([^"]*)"')
end

local function jsonNumber(json, key)
    local value = json:match('"' .. key .. '":(-?%d+)')
    if value == nil then
        return nil
    end
    return tonumber(value)
end

local function jsonBoolean(json, key)
    local value = json:match('"' .. key .. '":(true)')
    if value == 'true' then
        return true
    end
    value = json:match('"' .. key .. '":(false)')
    if value == 'false' then
        return false
    end
    return nil
end

local function colorVariable(name, alphaName)
    return SKIN:GetVariable(name) .. ',' .. SKIN:GetVariable(alphaName)
end

local function applyDisplay(statusText, batteryText, connectionText, powerText, level, color, batteryColor, statusColor)
    currentLevel = level
    SKIN:Bang('!SetOption', 'MeterStatus', 'Text', statusText)
    SKIN:Bang('!SetOption', 'MeterStatus', 'FontColor', statusColor or color)
    SKIN:Bang('!SetOption', 'MeterBattery', 'Text', batteryText)
    SKIN:Bang('!SetOption', 'MeterBattery', 'FontColor', batteryColor or color)
    SKIN:Bang('!SetOption', 'MeterConnection', 'Text', connectionText)
    SKIN:Bang('!SetOption', 'MeterPower', 'Text', powerText)
    SKIN:Bang('!SetOption', 'MeterBatteryBar', 'BarColor', color)
    SKIN:Bang('!UpdateMeterGroup', 'ControllerMeters')
    SKIN:Bang('!Redraw')
end

local function offlineDisplay()
    applyDisplay(
        'BRIDGE OFFLINE',
        '--',
        'Start Vader Battery Tray',
        '',
        0,
        colorVariable('colorInactiveButton', 'colorInactiveButtonAlpha'))
end

function Initialize()
    offlineDisplay()
end

function Update()
    if SKIN:GetVariable('BridgeOnline') ~= '1' then
        if lastSignature ~= 'offline' then
            lastSignature = 'offline'
            offlineDisplay()
        end
        return currentLevel
    end

    local measure = SKIN:GetMeasure('MeasureApi')
    local json = measure and measure:GetStringValue() or ''
    if json == '' then
        return currentLevel
    end

    local status = jsonString(json, 'status') or 'unavailable'
    local bandLevel = jsonNumber(json, 'bandLevel') or 0
    local band = jsonString(json, 'band')
    local charging = jsonBoolean(json, 'charging') or false
    local dockControllerState = jsonString(json, 'dockControllerState') or 'normal'
    local power = jsonString(json, 'power') or ''
    local connection = jsonString(json, 'connection') or 'Controller'

    local signature = table.concat({
        status,
        tostring(bandLevel),
        tostring(band),
        tostring(charging),
        dockControllerState,
        power,
        connection
    }, '|')

    if signature == lastSignature then
        return currentLevel
    end
    lastSignature = signature

    if status == 'starting' then
        applyDisplay(
            'STARTING',
            '--',
            'Waiting for first reading',
            '',
            0,
            colorVariable('colorInactiveButton', 'colorInactiveButtonAlpha'))
        return currentLevel
    end

    if status == 'disconnected' then
        applyDisplay(
            'RECEIVER DISCONNECTED',
            '--',
            'Reconnect receiver',
            '',
            0,
            colorVariable('colorInactiveButton', 'colorInactiveButtonAlpha'))
        return currentLevel
    end

    if status == 'receiver-disconnected' then
        applyDisplay(
            'RECEIVER DISCONNECTED',
            '--',
            'Reconnect receiver',
            '',
            0,
            colorVariable('colorInactiveButton', 'colorInactiveButtonAlpha'))
        return currentLevel
    end

    if status == 'controller-unavailable' then
        applyDisplay(
            'CONTROLLER ASLEEP / OFF',
            '--',
            'Wake with Guide',
            '',
            0,
            colorVariable('colorInactiveButton', 'colorInactiveButtonAlpha'))
        return currentLevel
    end

    if status ~= 'ok' then
        applyDisplay(
            'BATTERY UNAVAILABLE',
            '--',
            connection,
            power,
            0,
            colorVariable('colorInactiveButton', 'colorInactiveButtonAlpha'))
        return currentLevel
    end

    local batteryText = string.upper(band or 'UNKNOWN')
    local level = math.max(0, math.min(5, bandLevel)) * 20

    local barColor
    local batteryColor
    if bandLevel == 1 then
        barColor = '236,64,64,255'
        batteryColor = '236,64,64,255'
    elseif bandLevel == 2 then
        barColor = '255,140,0,255'
        batteryColor = '255,140,0,255'
    elseif bandLevel == 3 then
        barColor = '245,200,42,255'
        batteryColor = '245,200,42,255'
    elseif bandLevel == 4 then
        barColor = '91,248,128,255'
        batteryColor = '91,248,128,255'
    else
        barColor = '51,153,255,255'
        batteryColor = '51,153,255,255'
    end

    local stateText
    if dockControllerState == 'charge-sleep' then
        stateText = 'DOCK SLEEP'
    else
        stateText = charging and 'CHARGING' or (power == 'Charged' and 'CHARGED' or 'BATTERY')
    end
    local statusColor = barColor
    if charging and connection == 'Dock' then
        if bandLevel <= 2 then
            statusColor = '236,64,64,255'
        elseif bandLevel <= 4 then
            statusColor = '245,200,42,255'
        else
            statusColor = '51,153,255,255'
        end
    end

    applyDisplay(
        stateText,
        batteryText,
        connection,
        power,
        level,
        barColor,
        batteryColor,
        statusColor)
    return currentLevel
end
