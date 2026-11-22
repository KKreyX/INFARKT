using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CardLibrary", menuName = "Cards/Card Library")]
public class CardLibrary : ScriptableObject
{
    private static CardLibrary _instance;
    public static CardLibrary Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<CardLibrary>("CardLibrary");
            }
            return _instance;
        }
    }
    
    public List<ClassConfig> classConfigs;
    
    private Dictionary<int, ClassConfig> _classConfigDict;
    
    void OnEnable()
    {
        Initialize();
    }
    
    public void Initialize()
    {
        if (classConfigs == null) return;
        
        _classConfigDict = new Dictionary<int, ClassConfig>();
        foreach (ClassConfig config in classConfigs)
        {
            if (config != null)
            {
                _classConfigDict[config.classId] = config;
            }
        }
    }
    
    public ClassConfig GetClassConfig(int classId)
    {
        if (_classConfigDict == null || _classConfigDict.Count == 0) 
            Initialize();
        
        if (_classConfigDict.TryGetValue(classId, out ClassConfig config))
            return config;
        
        return null;
    }
}