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

local function applyDisplay(statusText, batteryText, connectionText, powerText, level, color, batteryColor)
    currentLevel = level
    SKIN:Bang('!SetOption', 'MeterStatus', 'Text', statusText)
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
    local percent = jsonNumber(json, 'percent')
    local estimated = jsonBoolean(json, 'estimated') or false
    local bandLevel = jsonNumber(json, 'bandLevel') or 0
    local band = jsonString(json, 'band')
    local charging = jsonBoolean(json, 'charging') or false
    local power = jsonString(json, 'power') or ''
    local connection = jsonString(json, 'connection') or 'Controller'
    local signature = table.concat({
        status,
        tostring(percent),
        tostring(estimated),
        tostring(bandLevel),
        tostring(band),
        tostring(charging),
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
            'Wake with Home',
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

    local batteryText
    local level
    if percent ~= nil then
        batteryText = tostring(percent) .. '%'
        level = math.max(0, math.min(100, percent))
    else
        batteryText = string.upper(band or 'UNKNOWN')
        if bandLevel == 1 then
            level = 33
        elseif bandLevel == 2 then
            level = 66
        elseif bandLevel == 3 then
            -- Dock EF reports a qualitative high band, not full charge.
            -- Use the matching controller step to avoid a docking-only jump.
            level = 80
        elseif bandLevel == 4 then
            level = 100
        else
            level = 0
        end
    end

    local barColor
    local batteryColor
    if bandLevel == 1 then
        barColor = '252,1,1,255'
        batteryColor = '252,1,1,255'
    elseif bandLevel == 2 then
        barColor = '227,182,18,255'
        batteryColor = '227,182,18,255'
    else
        barColor = '51,153,255,255'
        batteryColor = '51,153,255,255'
    end

    local stateText = charging and 'CHARGING' or (band == 'Full' and 'FULL' or 'BATTERY')
    applyDisplay(stateText, batteryText, connection, power, level, barColor, batteryColor)
    return currentLevel
end
