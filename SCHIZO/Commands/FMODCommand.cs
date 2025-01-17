using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FMOD.Studio;
using FMODUnity;
using SCHIZO.Commands.Attributes;
using SCHIZO.Commands.Base;
using SCHIZO.Commands.Output;
using SCHIZO.Helpers;
using UnityEngine;

namespace SCHIZO.Commands;

[Command(Name = "fmod",
    DisplayName = "FMOD",
    Description = "Commands for working with FMOD",
    RegisterConsoleCommand = true
)]
public class FMODCommand : CompositeCommand
{
    [SubCommand]
    public static object Play(string pathOrGuid, float distance = 0)
    {
        if (Guid.TryParse(pathOrGuid, out Guid guid))
            pathOrGuid = FMODHelpers.GetPath(guid);

        if (string.IsNullOrEmpty(pathOrGuid))
            return CommonResults.Error("Null sound path");

        try
        {
            if (distance <= 0 || !Camera.main)
            {
                FMODHelpers.PlayPath2D(pathOrGuid);
            }
            else
            {
                Vector3 deltaPos = UnityEngine.Random.onUnitSphere * distance;
                Vector3 pos = Camera.main.transform.position + deltaPos;
                RuntimeManager.PlayOneShot(pathOrGuid, pos);
            }
            return CommonResults.OK();
        }
        catch (EventNotFoundException)
        {
            return CommonResults.Error($"FMOD event not found: {pathOrGuid}");
        }
    }
    [SubCommand(NameOverride = "path")]
    public static object GetPath(string guid) => (object)FMODHelpers.GetPath(guid) ?? CommonResults.Error("Not found");
    [SubCommand(NameOverride = "id")]
    public static object GetId(string path) => (object)FMODHelpers.GetId(path) ?? CommonResults.Error("Not found");

    // all of these were adapted from https://discord.com/channels/324207629784186882/324207629784186882/1065010826571956294
    [SubCommand]
    public static string Banks(string bankFilter = null)
    {
        StringBuilder sb = new("Banks:\n");
        foreach (Bank bank in GetBanks(bankFilter))
        {
            bank.getPath(out string bankPath);
            bank.getID(out Guid bankId);
            sb.AppendLine($"{bankId} | {bankPath}");
        }
        LOGGER.LogMessage(sb.ToString());
        return "Logged all banks";
    }

    [SubCommand]
    public static string Buses(string bankFilter = null)
    {
        StringBuilder sb = new("FMOD bus list:\n");
        foreach (Bank bank in GetBanks(bankFilter))
        {
            bank.getPath(out string bankPath);
            bank.getBusList(out Bus[] busArray);
            if (busArray.Length == 0) continue;
            sb.AppendLine($"Buses in bank \"{bankPath}\"");
            foreach (Bus bus in busArray)
            {
                bus.getPath(out string busPath);
                bus.getID(out Guid busId);
                sb.AppendLine($"{busId} | {busPath}");
            }
        }
        LOGGER.LogMessage(sb.ToString());
        return "Logged all buses";
    }

    [SubCommand]
    public static string VCAs(string bankFilter = null)
    {
        StringBuilder sb = new("VCAs:\n");
        foreach (Bank bank in GetBanks(bankFilter))
        {
            bank.getPath(out string bankPath);
            bank.getVCAList(out VCA[] vcaArray);
            if (vcaArray.Length == 0) continue;
            sb.AppendLine($"VCAs in bank \"{bankPath}\"");
            foreach (VCA vca in vcaArray)
            {
                vca.getPath(out string vcaPath);
                vca.getID(out Guid vcaId);
                sb.AppendLine($"{vcaId} | {vcaPath}");
            }
        }
        LOGGER.LogMessage(sb.ToString());
        return "Logged all VCAs";
    }

    [SubCommand]
    public static string Events(string bankFilter = null)
    {
        StringBuilder sb = new("Events:\n");
        foreach (Bank bank in GetBanks(bankFilter))
        {
            bank.getPath(out string bankPath);
            bank.getEventList(out EventDescription[] eventArray);
            if (eventArray.Length == 0) continue;

            sb.AppendLine($"Events in bank \"{bankPath}\"");
            foreach (EventDescription eventDesc in eventArray)
            {
                eventDesc.getPath(out string eventPath);
                eventDesc.getID(out Guid eventId);
                sb.AppendLine($"{eventId} | {eventPath}");
            }
        }
        LOGGER.LogMessage(sb.ToString());
        return "Logged all events";
    }

    private static IEnumerable<Bank> GetBanks(string bankFilter = null)
    {
        RuntimeManager.StudioSystem.getBankList(out Bank[] banks);

        if (string.IsNullOrEmpty(bankFilter)) return banks;

        return banks.Where(b =>
        {
            b.getPath(out string path);
            b.getID(out Guid id);
            return path.Contains(bankFilter) || id.ToString().Contains(bankFilter);
        });
    }
}
