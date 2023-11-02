using System.Linq;
using Nautilus.Utility;
using SCHIZO.Creatures.Components;
using SCHIZO.Helpers;
using UnityEngine;

namespace SCHIZO.Creatures.Ermshark;

partial class ErmsharkAttack
{
    private void Awake()
    {
        attackSounds = attackSounds!?.Initialize(AudioUtils.BusPaths.UnderwaterCreatures);
    }

    public override void OnTouch(Collider collider)
    {
        if (!enabled) return;
        if (!liveMixin.IsAlive()) return;

        GameObject target = GetTarget(collider);

        if (global::CreatureData.GetCreatureType(gameObject) == global::CreatureData.GetCreatureType(target)) return;
        if (GetComponentsInParent<IOnMeleeAttack>().Any(handler => handler.HandleMeleeAttack(target))) return;

        Player player = target.GetComponent<Player>();
        if (player)
        {
            GameObject heldObject = Inventory.main.GetHeldObject();
            if (heldObject && canBeFed && player.CanBeAttacked() && TryEat(heldObject, true))
            {
                if (this.GetBiteSound()) Utils.PlayEnvSound(this.GetBiteSound(), mouth.transform.position);
                gameObject.SendMessage("OnMeleeAttack", heldObject, SendMessageOptions.DontRequireReceiver);
                return;
            }
        }

        if (CanBite(target))
        {
            timeLastBite = Time.time;
            LiveMixin living = target.GetComponent<LiveMixin>();
            if (living && living.IsAlive())
            {
                living.TakeDamage(GetBiteDamage(target), default, DamageType.Normal, gameObject);
                living.NotifyCreatureDeathsOfCreatureAttack();
            }
            Vector3 damageFxPos = collider.ClosestPointOnBounds(mouth.transform.position);
            if (damageFX) Instantiate(damageFX, damageFxPos, damageFX.transform.rotation);
            if (this.GetBiteSound()) Utils.PlayEnvSound(this.GetBiteSound(), damageFxPos);

            if (attackSounds) attackSounds.Play(emitter);

            creature.Aggression.Add(-biteAggressionDecrement);
            if (living && !living.IsAlive())
            {
                TryEat(living.gameObject);
            }
            gameObject.SendMessage("OnMeleeAttack", target, SendMessageOptions.DontRequireReceiver);
        }
    }
}
