function filter(tag, timestamp, record)
    if record["kubernetes"] == nil then
        return 0, 0, 0
    end
    if string.starts(record["kubernetes"]["pod_name"], "neon-log") then
        return -1, 0, 0
    end
    if string.starts(record["kubernetes"]["pod_name"], "fluent") then
        return -1, 0, 0
    end
    return 0, 0, 0
end

function dedot(tag, timestamp, record)
    if record["kubernetes"] == nil then
        return 0, 0, 0
    end
    dedot_keys(record["kubernetes"]["annotations"])
    dedot_keys(record["kubernetes"]["labels"])
    return 1, timestamp, record
end

function dedot_keys(map)
    if map == nil then
        return
    end
    local new_map = {}
    local changed_keys = {}
    for k, v in pairs(map) do
        local deslashed = string.gsub(k, "%/", "_")
        local dedotted = string.gsub(deslashed, "%.", "_")
        if dedotted ~= k then
            new_map[dedotted] = v
            changed_keys[k] = true
        end
    end
    for k in pairs(changed_keys) do
        map[k] = nil
    end
    for k, v in pairs(new_map) do
        map[k] = v
    end
end

function string.starts(String,Start)
   return string.sub(String,1,string.len(Start))==Start
end