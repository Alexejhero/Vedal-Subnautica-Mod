using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SCHIZO.Commands.Base;
using SCHIZO.Commands.Context;
using SCHIZO.Commands.Output;
using SCHIZO.Creatures.Components;
using SCHIZO.Creatures.Ermfish;
using SCHIZO.Helpers;
using SCHIZO.Sounds.Players;
using UnityEngine;
using UWE;

namespace SCHIZO.SwarmControl.Redeems.Spawns;

#nullable enable
[Redeem(Name = "redeem_bigerm",
    DisplayName = "Big Erm",
    Description = "It's time to Get Big",
    RegisterConsoleCommand = true
    )]
internal class BigErm : Command, IParameters
{
    public IReadOnlyList<Parameter> Parameters => [];

    private static readonly int Scale = 5;
    private static readonly float Pitch = -2.0f;
    private static GameObject? _ermfishPrefab;
    private static TechType _ermfishTechType;

    protected override object? ExecuteCore(CommandExecutionContext ctx)
    {
        if (!Player.main) return CommonResults.Error("Requires a loaded game");

        if (_ermfishTechType is default(TechType))
            _ermfishTechType = (TechType) Enum.Parse(typeof(TechType), "ermfish");

        GameObject ermfish = PhysicsHelpers.ObjectsInRange(Player.main.transform.position, 100)
            .OfTechType(_ermfishTechType)
            .SelectComponentInParent<PrefabIdentifier>() // sphere cast hits the collider which is on the model
            .Select(tag => tag.gameObject)
            .FirstOrDefault(erm =>
            {
                Carryable carryable = erm.GetComponent<Carryable>();
                if (carryable && carryable.isCarried) return false;

                if (erm.transform.localScale.x > 1) return false;

                return true;
            });

        if (ermfish)
        {
            GetBigAndWinWildPrizes(ermfish);
        }
        else
        {
            CoroutineHost.StartCoroutine(SpawnCoro());
        }

        return CommonResults.OK();
    }

    private IEnumerator SpawnCoro()
    {
        if (!_ermfishPrefab)
        {
            IPrefabRequest request = PrefabDatabase.GetPrefabAsync(CraftData.GetClassIdForTechType(_ermfishTechType));
            yield return request;
            if (request.TryGetPrefab(out GameObject prefab))
                _ermfishPrefab = prefab;
            else
                yield break;
        }
        yield return new WaitUntil(() => SwarmControlManager.Instance.CanSpawn);

        GameObject bigErm = Utils.CreatePrefab(_ermfishPrefab);
        yield return null;
        GetBigAndWinWildPrizes(bigErm);
    }

    private void GetBigAndWinWildPrizes(GameObject bigErm)
    {
        bigErm.transform.localScale = new(Scale, Scale, Scale);
        bigErm.GetComponent<Pickupable>().isPickupable = false;
        bigErm.GetComponent<Carryable>().enabled = false;
        bigErm.GetComponent<CarryCreature>().enabled = false;
        bigErm.GetComponent<ErmStack>().enabled = false;
        bigErm.GetComponentsInChildren<SoundPlayer>()
            .ForEach(plr =>
            {
                plr.onPlay.AddListener((evt) =>
                    evt.setParameterByName("Pitch", Pitch).CheckResult()
                );
            });
    }
}
