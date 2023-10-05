﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SCHIZO.Extensions;
public static class LinqExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TComponent> SelectComponent<TComponent>(this IEnumerable<GameObject> gameObjects) where TComponent : Component
        => gameObjects
            .Select(gameObj => gameObj.GetComponent<TComponent>())
            .Where(comp => comp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TComponent> SelectComponentInParent<TComponent>(this IEnumerable<GameObject> gameObjects) where TComponent : Component
        => gameObjects
            .Select(gameObj => gameObj.GetComponentInParent<TComponent>())
            .Where(comp => comp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<GameObject> WithComponent<TComponent>(this IEnumerable<GameObject> gameObjects) where TComponent : Component
        => gameObjects.Where(gameObj => gameObj.GetComponent<TComponent>());


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<GameObject> OfTechType(this IEnumerable<GameObject> gameObjects, TechType techType)
        => gameObjects.Where(gameObj => CraftData.GetTechType(gameObj) == techType);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<GameObject> OfTechType(this IEnumerable<GameObject> gameObjects, ICollection<TechType> techTypes)
        => gameObjects.Where(gameObj => techTypes.Contains(CraftData.GetTechType(gameObj)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IOrderedEnumerable<GameObject> OrderByDistanceTo(this IEnumerable<GameObject> gameObjects, GameObject target)
        => gameObjects.OrderBy(gameObj => gameObj.transform.position.DistanceSqrXZ(target.transform.position));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IOrderedEnumerable<GameObject> OrderByDistanceTo(this IEnumerable<GameObject> gameObjects, Vector3 target)
        => gameObjects.OrderBy(gameObj => gameObj.transform.position.DistanceSqrXZ(target));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IOrderedEnumerable<GameObject> OrderByDistanceToDescending(this IEnumerable<GameObject> gameObjects, GameObject target)
        => gameObjects.OrderByDescending(gameObj => gameObj.transform.position.DistanceSqrXZ(target.transform.position));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IOrderedEnumerable<GameObject> OrderByDistanceToDescending(this IEnumerable<GameObject> gameObjects, Vector3 target)
        => gameObjects.OrderByDescending(gameObj => gameObj.transform.position.DistanceSqrXZ(target));
}
