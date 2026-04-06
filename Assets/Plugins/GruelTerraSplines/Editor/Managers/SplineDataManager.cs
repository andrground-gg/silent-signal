using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;

namespace GruelTerraSplines.Managers
{
    /// <summary>
    /// Manages spline data hierarchy and list management.
    /// Handles spline item creation, hierarchy tracking, and foldout state persistence.
    /// </summary>
    public class SplineDataManager : IWindowManager
    {
        private EditorWindow window;
        
        public List<IListItemData> SplineItems { get; private set; } = new List<IListItemData>();
        
        // Foldout state persistence
        private Dictionary<int, bool> splineFoldoutStates = new Dictionary<int, bool>();
        private Dictionary<int, bool> groupFoldoutStates = new Dictionary<int, bool>();
        
        public event Action DataChanged;
        
        public void Initialize(EditorWindow window)
        {
            this.window = window;
        }
        
        public void OnDestroy()
        {
            SplineItems.Clear();
            splineFoldoutStates.Clear();
            groupFoldoutStates.Clear();
        }
        
        public void RefreshChildren(Terrain terrain, Transform splineGroup)
        {
            if (splineGroup == null || splineGroup.gameObject == null)
            {
                SplineItems.Clear();
                DataChanged?.Invoke();
                return;
            }
            
            // Save existing foldout states before clearing
            SaveFoldoutStates();
            
            // Clear existing items
            SplineItems.Clear();
            
            // Collect all valid spline containers (including nested ones)
            var splineContainers = splineGroup.GetComponentsInChildren<SplineContainer>();
            
            if (splineContainers.Length == 0)
            {
                DataChanged?.Invoke();
                return;
            }
            
            // Collect all direct children of the spline group (both individual splines and group transforms)
            var directChildren = new List<(Transform transform, bool isSpline, SplineHierarchyInfo splineInfo)>();
            
            // Get all direct children of splineGroup
            for (int i = 0; i < splineGroup.childCount; i++)
            {
                Transform child = splineGroup.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                
                // Check if this child is a SplineContainer
                var splineContainer = child.GetComponent<SplineContainer>();
                if (splineContainer != null)
                {
                    // This is an individual spline
                    directChildren.Add((child, true, new SplineHierarchyInfo
                    {
                        container = splineContainer,
                        parent = splineGroup,
                        siblingIndex = child.GetSiblingIndex()
                    }));
                }
                else
                {
                    // This might be a group - check if it contains any SplineContainers
                    var splinesInGroup = child.GetComponentsInChildren<SplineContainer>();
                    if (splinesInGroup.Length > 0)
                    {
                        // This is a group transform
                        directChildren.Add((child, false, null));
                    }
                }
            }
            
            // Sort direct children by sibling index to maintain hierarchy order
            directChildren.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            
            // Build final ordered list maintaining exact hierarchy order
            foreach (var childInfo in directChildren)
            {
                if (childInfo.isSpline)
                {
                    // Add individual spline
                    var item = CreateSplineItemData(childInfo.splineInfo.container);
                    SplineItems.Add(item);
                }
                else
                {
                    // Add group
                    Transform groupTransform = childInfo.transform;
                    string hierarchyPath = GetHierarchyPath(groupTransform, splineGroup);
                    
                    var groupItem = new GroupItemData
                    {
                        groupTransform = groupTransform,
                        hierarchyPath = hierarchyPath,
                        isFoldoutExpanded = GetSavedGroupFoldoutState(groupTransform)
                    };
                    
                    // Get all splines in this group and sort them by sibling index
                    var splinesInGroup = groupTransform.GetComponentsInChildren<SplineContainer>();
                    var groupSplineInfos = new List<SplineHierarchyInfo>();
                    
                    foreach (var spline in splinesInGroup)
                    {
                        if (spline != null && spline.gameObject.activeInHierarchy)
                        {
                            groupSplineInfos.Add(new SplineHierarchyInfo
                            {
                                container = spline,
                                parent = groupTransform,
                                siblingIndex = spline.transform.GetSiblingIndex()
                            });
                        }
                    }
                    
                    // Sort splines within group by sibling index
                    groupSplineInfos.Sort((a, b) => a.siblingIndex.CompareTo(b.siblingIndex));
                    
                    // Add splines to group
                    foreach (var splineInfo in groupSplineInfos)
                    {
                        var splineItem = CreateSplineItemData(splineInfo.container);
                        groupItem.splines.Add(splineItem);
                    }
                    
                    SplineItems.Add(groupItem);
                }
            }
            
            DataChanged?.Invoke();
        }
        
        public List<SplineItemData> GetAllSplineItems()
        {
            var allSplines = new List<SplineItemData>();
            
            foreach (var item in SplineItems)
            {
                if (item is SplineItemData splineItem)
                {
                    allSplines.Add(splineItem);
                }
                else if (item is GroupItemData groupItem)
                {
                    allSplines.AddRange(groupItem.splines);
                }
            }
            
            return allSplines;
        }
        
        public bool HasActiveSplines()
        {
            var allSplines = GetAllSplineItems();
            foreach (var item in allSplines)
            {
                if (item.container != null && item.container.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }
        
        public SplineItemData GetSplineItemByIndex(int index)
        {
            int currentIndex = 0;
            
            foreach (var item in SplineItems)
            {
                if (item is SplineItemData splineItem)
                {
                    if (currentIndex == index) return splineItem;
                    currentIndex++;
                }
                else if (item is GroupItemData groupItem)
                {
                    if (index >= currentIndex && index < currentIndex + groupItem.splines.Count)
                    {
                        return groupItem.splines[index - currentIndex];
                    }
                    currentIndex += groupItem.splines.Count;
                }
            }
            
            return null;
        }
        
        public int GetGlobalSplineIndex(SplineItemData targetSpline)
        {
            int currentIndex = 0;
            
            foreach (var item in SplineItems)
            {
                if (item is SplineItemData splineItem)
                {
                    if (splineItem == targetSpline) return currentIndex;
                    currentIndex++;
                }
                else if (item is GroupItemData groupItem)
                {
                    for (int i = 0; i < groupItem.splines.Count; i++)
                    {
                        if (groupItem.splines[i] == targetSpline) return currentIndex;
                        currentIndex++;
                    }
                }
            }
            
            return -1; // Not found
        }
        
        private SplineItemData CreateSplineItemData(SplineContainer container)
        {
            // Get or add SplineTerrainSettings component
            var settingsComponent = container.GetComponent<TerrainSplineSettings>();
            if (settingsComponent == null)
            {
                settingsComponent = container.gameObject.AddComponent<TerrainSplineSettings>();
            }

            return new SplineItemData 
            { 
                container = container, 
                settings = settingsComponent.settings,
                isFoldoutExpanded = GetSavedSplineFoldoutState(container)
            };
        }
        
        private string GetHierarchyPath(Transform splineTransform, Transform rootTransform)
        {
            if (splineTransform == null || rootTransform == null) return "Unknown";
            
            var pathParts = new List<string>();
            Transform current = splineTransform;
            
            // Walk up the hierarchy until we reach the root spline group
            while (current != null && current != rootTransform)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }
            
            // Reverse the list to get the correct order (root to leaf)
            pathParts.Reverse();
            
            // Join with "/" separator
            return string.Join("/", pathParts);
        }
        
        private void SaveFoldoutStates()
        {
            splineFoldoutStates.Clear();
            groupFoldoutStates.Clear();
            
            foreach (var item in SplineItems)
            {
                if (item is SplineItemData splineItem && splineItem.container != null)
                {
                    int instanceId = splineItem.container.GetInstanceID();
                    splineFoldoutStates[instanceId] = splineItem.isFoldoutExpanded;
                }
                else if (item is GroupItemData groupItem && groupItem.groupTransform != null)
                {
                    int instanceId = groupItem.groupTransform.GetInstanceID();
                    groupFoldoutStates[instanceId] = groupItem.isFoldoutExpanded;
                }
            }
        }
        
        private bool GetSavedSplineFoldoutState(SplineContainer container)
        {
            if (container == null) return true; // Default to expanded
            
            int instanceId = container.GetInstanceID();
            return splineFoldoutStates.TryGetValue(instanceId, out bool state) ? state : true;
        }
        
        private bool GetSavedGroupFoldoutState(Transform groupTransform)
        {
            if (groupTransform == null) return true; // Default to expanded
            
            int instanceId = groupTransform.GetInstanceID();
            return groupFoldoutStates.TryGetValue(instanceId, out bool state) ? state : true;
        }
    }
    
    // Helper class to track hierarchy position for proper ordering
    public class SplineHierarchyInfo
    {
        public SplineContainer container;
        public Transform parent;
        public int siblingIndex;
    }
}

