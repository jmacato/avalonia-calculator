// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class UnitToUnitToConversionDataMap
{
    private Dictionary<Unit, Dictionary<Unit, ConversionData>> _map;

    public UnitToUnitToConversionDataMap()
    {
        _map = new Dictionary<Unit, Dictionary<Unit, ConversionData>>(new UnitHash());
    }

    public Dictionary<Unit, ConversionData> GetOrAdd(Unit key)
    {
        if (!_map.TryGetValue(key, out var innerMap))
        {
            innerMap = new Dictionary<Unit, ConversionData>(new UnitHash());
            _map[key] = innerMap;
        }

        return innerMap;
    }

    public void Set(Unit key, Dictionary<Unit, ConversionData> value) => _map[key] = value;

    public bool ContainsKey(Unit key) => _map.ContainsKey(key);

    public void Add(Unit key, Dictionary<Unit, ConversionData> value) => _map.Add(key, value);

    public bool TryGetValue(Unit key, out Dictionary<Unit, ConversionData> value) =>
        _map.TryGetValue(key, out value);

    public void Clear()
    {
        _map.Clear();
    }
}
