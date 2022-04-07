using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
[GenerateAuthoringComponent]
public struct SelectableSoldierTag : IComponentData { }
public struct SelectedEntityTag : IComponentData {}