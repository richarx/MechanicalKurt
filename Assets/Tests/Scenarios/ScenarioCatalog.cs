#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;

public partial class ScenarioRunner
{
    /// <summary>Tous les scénarios, dans l'ordre d'exécution : c'est le seul endroit où en déclarer un nouveau.</summary>
    static readonly ScenarioDef[] Catalog =
    {
        Solo("Anneau de boost", r => r.RingBoost()),
        Solo("Tuto : zone de départ", r => r.TutoStopZone()),
        Solo("Course : départ à 2 pigeons", r => r.RaceStart()),
        Solo("Course : échec de hauteur", r => r.RaceHeightFailure()),
        Solo("Course : une seule à la fois", r => r.RaceLockedWhileRunning()),
        Solo("Course : victoire et course refaite", r => r.RaceWinAndReplay()),
        Solo("Course : un seul joueur suffit (AnyPigeon)", r => r.RaceAnyPigeon()),
        Solo("Ragdoll : les zones ne se déclenchent pas", r => r.RagdollIgnoredByZones()),
        Solo("Ragdoll : le pigeon retrouve sa forme en se relevant", r => r.RagdollRecovers()),
        Solo("Mission : livraison simple", r => r.RunDeliveryMission()),
        Solo("Mission : collecte (Spidey)", r => r.RunCollectionMission()),
        Solo("Mission : billboards", r => r.RunBillboardMission()),
        Solo("Mission : prière (Roar)", r => r.RunPrayerMission()),
        Solo("Équipement : ramassage", r => r.RunEquipmentPickup()),
        Solo("Eau : speedway", r => r.RunWaterSpeedway()),
        Solo("Ballon de plage : support mobile", r => r.RunBeachBall()),
        Solo("Eau : profondeur maximale", r => r.RunMaxDepth()),
        Solo("Caméras : sous l'eau et précision", r => r.RunCameras()),
        Solo("Équipement : lunettes", r => r.RunSunglasses()),
        Solo("Anneaux des égouts : boost et combo", r => r.RunSewerRings()),
        Solo("Voitures : solides en solo, on roule dessus", r => r.RunCarRide()),
        Solo("Voitures : toutes créées, visibles et en route", r => r.TrafficIsRunning()),
        Solo("Voitures : mouvement fluide entre deux ticks réseau", r => r.TrafficIsSmooth()),
        Solo("Voitures : photos d'une voiture et du pigeon posé dessus", r => r.PigeonOnCar()),
        Solo("Sons : 2D pour soi, 3D pour les autres", r => r.RunPigeonAudio()),
        Solo("Pause : le jeu continue, le pigeon reste en l'air", r => r.PauseKeepsGameRunning()),
        Solo("Endurance et vol stationnaire (monter, descendre)", r => r.StaminaAndHover()),
        Solo("Pentes : marcher sans décoller", r => r.WalkOnSlopes()),
        Solo("Course chronométrée : anneaux, chrono, place, classement", r => r.TimedRaceSolo()),
        Solo("Nuit : lampadaires et phares allumés, éteints le jour", r => r.NightLights()),
        Solo("Roucoulement : bulle pour les autres, au bord de l'écran si loin", r => r.CooBubbles()),
        Solo("Autres pigeons : traînée colorée et point discret", r => r.SeeOtherPigeons()),

        Multi("Multi : départ de course à 2 joueurs", r => r.MultiRaceStart()),
        Multi("Multi : victoire, gel et course refaite", r => r.MultiRaceWin()),
        Multi("Multi : échec de hauteur", r => r.MultiRaceHeight()),
        Multi("Multi : arrivée en pleine course", r => r.MultiLateJoin()),
        Multi("Multi : un seul suffit, lancé par le second joueur", r => r.MultiAnyStartedByRemote()),
        Multi("Multi : un seul suffit, lancé par l'hôte", r => r.MultiAnyStartedByHost()),
        Multi("Multi : décompte vu par l'autre joueur, qui le rejoint", r => r.MultiJoinCountdown()),
        Multi("Multi : tuto local au second joueur", r => r.MultiTuto()),
        Multi("Multi : équipement du second joueur", r => r.MultiEquipment()),
        Multi("Multi : sons chez le second joueur", r => r.MultiAudio()),
        Multi("Multi : effets d'anneau seulement pour celui qui le traverse", r => r.MultiRingFeedback()),
        Multi("Multi : pause sans arrêt de la partie, même heure du jour", r => r.MultiPauseAndDayNight()),
        Multi("Multi : roucoulement vu par l'autre joueur, à sa couleur", r => r.MultiCoo()),
        Multi("Multi : mêmes voitures chez tous, solides, on roule dessus", r => r.MultiTrafficShared()),
        Multi("Multi : course chronométrée à deux, classement chez chacun", r => r.MultiTimedRace()),
    };

    class ScenarioDef
    {
        public string Name;
        public Func<ScenarioRunner, IEnumerator> Body;
        /// <summary>Joué avec un vrai second joueur (Player 2 de Multiplayer Play Mode) ; sinon l'hôte seul, avec des bots.</summary>
        public bool Multi;
    }

    static ScenarioDef Solo(string name, Func<ScenarioRunner, IEnumerator> body) => new ScenarioDef { Name = name, Body = body };

    static ScenarioDef Multi(string name, Func<ScenarioRunner, IEnumerator> body) => new ScenarioDef { Name = name, Body = body, Multi = true };

    public static IEnumerable<string> SoloScenarioNames => NamesWhere(multi: false);

    public static IEnumerable<string> MultiScenarioNames => NamesWhere(multi: true);

    public static bool IsMultiScenario(string name) => Array.Exists(Catalog, def => def.Multi && def.Name == name);

    static IEnumerable<string> NamesWhere(bool multi)
    {
        foreach (ScenarioDef def in Catalog)
            if (def.Multi == multi)
                yield return def.Name;
    }
}
#endif
