using System.Collections.Generic;

/// <summary>
/// Central localized string constants and helper methods for Deep Space Pioneers game UI and status messages.
/// </summary>
public static class UIStrings
{
    // --- Title & General UI ---
    public static readonly LocalizedString GameTitle =
        new LocalizedString("Deep Space Pioneers", "Deep Space Pioneers");

    public static readonly LocalizedString NextTurn =
        new LocalizedString("Next", "Weiter");

    public static readonly LocalizedString Cancel =
        new LocalizedString("Cancel", "Abbrechen");

    public static readonly LocalizedString QuitGame =
        new LocalizedString("Quit Game", "Spiel beenden");

    public static readonly LocalizedString Quit =
        new LocalizedString("Quit", "Beenden");

    public static readonly LocalizedString ConfirmQuitPrompt =
        new LocalizedString("Do you really want to quit the game and return to the desktop?", "Möchtest du das Spiel wirklich beenden und zum Desktop zurückkehren?");

    public static readonly LocalizedString DefaultPlayerName =
        new LocalizedString("Player {0}", "Spieler {0}");

    public static readonly LocalizedString DiscoveryTestPlayerName =
        new LocalizedString("Discovery Test", "Erkundungstest");

    // --- Sidebar Panels ---
    public static readonly LocalizedString ResourcesHeading =
        new LocalizedString("Resources", "Rohstoffe");

    public static readonly LocalizedString FlagshipHeading =
        new LocalizedString("Flagship", "Flaggschiff");

    // --- Resources ---
    public static readonly LocalizedString ResourceOre =
        new LocalizedString("Ore", "Erz");

    public static readonly LocalizedString ResourceFuel =
        new LocalizedString("Fuel", "Treibstoff");

    public static readonly LocalizedString ResourceCarbon =
        new LocalizedString("Carbon", "Carbon");

    public static readonly LocalizedString ResourceFood =
        new LocalizedString("Food", "Nahrung");

    public static readonly LocalizedString ResourceGoods =
        new LocalizedString("Goods", "Handelsware");

    public static readonly LocalizedString HalfMedals =
        new LocalizedString("Fame Medal Pieces: ", "Halbe Medaillen: ");

    public static string GetResourceName(ResourceType resource)
    {
        switch (resource)
        {
            case ResourceType.Ore: return ResourceOre.GetText();
            case ResourceType.Fuel: return ResourceFuel.GetText();
            case ResourceType.Carbon: return ResourceCarbon.GetText();
            case ResourceType.Food: return ResourceFood.GetText();
            case ResourceType.TradeGoods: return ResourceGoods.GetText();
            default: return resource.ToString();
        }
    }

    // --- Flagship Upgrades & Flight Impulse Cores ---
    public static readonly LocalizedString Boosters =
        new LocalizedString("Boosters", "Antriebe");

    public static readonly LocalizedString Cannons =
        new LocalizedString("Cannons", "Bordkanonen");

    public static readonly LocalizedString FreightPods =
        new LocalizedString("Freight Pods", "Frachtmodule");

    public static readonly LocalizedString FameMedals =
        new LocalizedString("Fame Medals", "Medaillen");

    public static string GetUpgradeName(FlagshipUpgrade upgrade)
    {
        switch (upgrade)
        {
            case FlagshipUpgrade.Drive: return Boosters.GetText();
            case FlagshipUpgrade.Cannon: return Cannons.GetText();
            case FlagshipUpgrade.CargoModule: return FreightPods.GetText();
            default: return upgrade.ToString();
        }
    }

    public static readonly LocalizedString CoreBlack =
        new LocalizedString("Black", "Schwarz");

    public static readonly LocalizedString CoreBlue =
        new LocalizedString("Blue", "Blau");

    public static readonly LocalizedString CoreYellow =
        new LocalizedString("Yellow", "Gelb");

    public static readonly LocalizedString CoreRed =
        new LocalizedString("Red", "Rot");

    public static string GetCoreName(FlightImpulseCore core)
    {
        switch (core)
        {
            case FlightImpulseCore.Black: return CoreBlack.GetText();
            case FlightImpulseCore.Blue: return CoreBlue.GetText();
            case FlightImpulseCore.Yellow: return CoreYellow.GetText();
            case FlightImpulseCore.Red: return CoreRed.GetText();
            default: return core.ToString();
        }
    }

    public static readonly LocalizedString FlightImpulseSummary =
        new LocalizedString("Impulse: {0} + {1} = {2} + {3} Boosters = {4}", "Flugimpuls: {0} + {1} = {2} + {3} Antriebe = {4}");

    public static readonly LocalizedString EncounterPending =
        new LocalizedString("Resolve encounter first", "Begegnung zuerst abhandeln");

    public static readonly LocalizedString EncounterResolved =
        new LocalizedString("Encounter resolved", "Begegnung abgehandelt");

    // --- Ship Types ---
    public static readonly LocalizedString ColonyShip =
        new LocalizedString("Colony Ship", "Kolonieschiff");

    public static readonly LocalizedString TradeShip =
        new LocalizedString("Trade Ship", "Handelsschiff");

    public static string GetShipName(StarshipType shipType)
    {
        return shipType == StarshipType.ColonyShip ? ColonyShip.GetText() : TradeShip.GetText();
    }

    // --- Turn Phases ---
    public static readonly LocalizedString PhasePrefix =
        new LocalizedString("Phase: ", "Phase: ");

    public static readonly LocalizedString PhaseSetup =
        new LocalizedString("Set-up", "Gründung");

    public static readonly LocalizedString PhaseProduction =
        new LocalizedString("Production", "Ertrag");

    public static readonly LocalizedString PhaseTradeAndBuild =
        new LocalizedString("Trade & Build", "Handel & Bau");

    public static readonly LocalizedString PhaseFlight =
        new LocalizedString("Flight", "Flug");

    public static string GetTurnPhaseName(GameTurnPhase phase)
    {
        switch (phase)
        {
            case GameTurnPhase.Production: return PhaseProduction.GetText();
            case GameTurnPhase.TradeAndBuild: return PhaseTradeAndBuild.GetText();
            case GameTurnPhase.Flight: return PhaseFlight.GetText();
            default: return phase.ToString();
        }
    }

    public static readonly LocalizedString ProductionRollSummary =
        new LocalizedString("  |  Production Roll: {0}", "  |  Ertragswurf: {0}");

    // --- Turn Status & Ship Building ---
    public static readonly LocalizedString StatusSetupColony =
        new LocalizedString("Set-up · Colony {0}\nClick an unoccupied colony site", "Gründung · Kolonie {0}\nKlicke auf freien Bauplatz");

    public static readonly LocalizedString StatusSetupSpaceport =
        new LocalizedString("Set-up · Spaceport\nClick your own colony", "Gründung · Raumhafen\nKlicke auf eigene Kolonie");

    public static readonly LocalizedString StatusSetupStartingShip =
        new LocalizedString("Set-up · Starting Ship\nClick one of the two marked sites", "Gründung · Startschiff\nKlicke auf einen der zwei markierten Punkte");

    public static readonly LocalizedString StatusPlacingShip =
        new LocalizedString("Build Phase · {0}\nClick an unoccupied spaceport site", "Bauphase · {0}\nKlicke auf freien Raumhafenpunkt");

    public static readonly LocalizedString ShipBuildSelectInstruction =
        new LocalizedString("Select a ship, then click an unoccupied spaceport site.", "Schiff auswählen und danach einen freien Raumhafenpunkt anklicken.");

    public static readonly LocalizedString ShipBuildPlacingInstruction =
        new LocalizedString("Click an unoccupied spaceport site for the {0}.", "Klicke auf einen freien Raumhafenpunkt für das {0}.");

    public static readonly LocalizedString BuildColonyShipCost =
        new LocalizedString("Colony Ship  ·  1 Ore, 1 Fuel,\n1 Carbon, 1 Food", "Kolonieschiff  ·  1 Erz, 1 Treibstoff,\n1 Carbon, 1 Nahrung");

    public static readonly LocalizedString BuildTradeShipCost =
        new LocalizedString("Trade Ship  ·  1 Ore, 1 Fuel,\n2 Goods", "Handelsschiff  ·  1 Erz, 1 Treibstoff,\n2 Handelsware");

    public static readonly LocalizedString BuildSpaceportCost =
        new LocalizedString("Spaceport  ·  3 Carbon, 2 Food", "Raumhafen  ·  3 Carbon, 2 Nahrung");

    public static readonly LocalizedString BuildBoosterCost =
        new LocalizedString("Booster  ·  2 Fuel", "Antrieb  ·  2 Treibstoff");

    public static readonly LocalizedString BuildCannonCost =
        new LocalizedString("Cannon  ·  2 Carbon", "Bordkanone  ·  2 Carbon");

    public static readonly LocalizedString BuildFreightPodCost =
        new LocalizedString("Freight Pod  ·  2 Ore", "Frachtmodul  ·  2 Erz");

    public static readonly LocalizedString Colony =
        new LocalizedString("Colony", "Kolonie");

    public static readonly LocalizedString Colonies =
        new LocalizedString("Colonies", "Kolonien");

    public static readonly LocalizedString Spaceport =
        new LocalizedString("Spaceport", "Raumhafen");

    public static readonly LocalizedString Spaceports =
        new LocalizedString("Spaceports", "Raumhäfen");

    public static readonly LocalizedString TradeStation =
        new LocalizedString("Trade Station", "Handelsstation");

    public static readonly LocalizedString TradeStations =
        new LocalizedString("Trade Stations", "Handelsstationen");

    public static readonly LocalizedString TransportShips =
        new LocalizedString("Transport Ships", "Transportschiffe");

    public static readonly LocalizedString Booster =
        new LocalizedString("Booster", "Antrieb");

    public static readonly LocalizedString Cannon =
        new LocalizedString("Cannon", "Bordkanone");

    public static readonly LocalizedString FreightPod =
        new LocalizedString("Freight Pod", "Frachtmodul");

    public static string GetUpgradeSingularName(FlagshipUpgrade upgrade)
    {
        switch (upgrade)
        {
            case FlagshipUpgrade.Drive: return Booster.GetText();
            case FlagshipUpgrade.Cannon: return Cannon.GetText();
            case FlagshipUpgrade.CargoModule: return FreightPod.GetText();
            default: return upgrade.ToString();
        }
    }

    public static readonly LocalizedString TradeAndBuildInstruction =
        new LocalizedString("Trade & Build: Choose a construction or trade.", "Handeln & Bauen: Wähle Bauaktion oder Handel.");

    public static readonly LocalizedString StatusPlacingSpaceport =
        new LocalizedString("Build Phase · Spaceport\nClick one of your colonies to upgrade", "Bauphase · Raumhafen\nKlicke auf eigene Kolonie zum Ausbau");

    public static readonly LocalizedString StatusSpaceportBuilt =
        new LocalizedString("{0} upgraded the colony at {1} to a spaceport.", "{0} hat die Kolonie bei {1} zum Raumhafen ausgebaut.");

    public static readonly LocalizedString StatusUpgradeBuilt =
        new LocalizedString("{0} built a {1} (total: {2}).", "{0} hat einen {1} gebaut (gesamt: {2}).");

    public static readonly LocalizedString StatusShipBuilt =
        new LocalizedString("{0} launched a {1}.", "{0} hat ein {1} gestartet.");

    public static readonly LocalizedString StatusTradeStationEstablished =
        new LocalizedString("{0} established a trade station at {1}.", "{0} hat eine Handelsstation bei {1} errichtet.");

    public static readonly LocalizedString StatusColonyFounded =
        new LocalizedString("{0} founded a colony at {1}.", "{0} hat eine Kolonie bei {1} gegründet.");

    public static readonly LocalizedString FoundColonyButton =
        new LocalizedString("Found Colony", "Kolonie gründen");

    public static readonly LocalizedString EstablishTradeStationButton =
        new LocalizedString("Establish Trade Station", "Handelsstation errichten");

    public static readonly LocalizedString ShipStatusInfo =
        new LocalizedString("{0}  ·  Movement: {1}/{2}", "{0}  ·  Bewegung: {1}/{2}");

    public static readonly LocalizedString FlightPhaseInstruction =
        new LocalizedString("Flight Phase: Select a ship, then right-click destination to move.", "Flugphase: Schiff auswählen, Ziel mit Rechtsklick ansteuern.");

    public static readonly LocalizedString ShipReadyToFoundColony =
        new LocalizedString("Colony ship is at a colony site.", "Kolonieschiff befindet sich auf einem Koloniebauplatz.");

    public static readonly LocalizedString ShipReadyToEstablishTradeStation =
        new LocalizedString("Trade ship is on the docking point of an outpost.", "Handelsschiff befindet sich auf dem Andockpunkt eines Außenpostens.");

    public static readonly LocalizedString TradeShipNeedsFreightPods =
        new LocalizedString("Not enough freight pods to dock at this outpost (have {0}, need at least {1} because {2} station(s) already exist).", "Nicht genügend Frachtmodule für diesen Außenposten (vorhanden: {0}, benötigt: mind. {1}, da bereits {2} Station(en) existieren).");

    public static readonly LocalizedString OutpostDocksFull =
        new LocalizedString("All 5 docks at this alien trade outpost are occupied.", "Alle 5 Docks dieses Außenpostens sind belegt.");

    public static readonly LocalizedString ColonyShipCannotDock =
        new LocalizedString("Colony ships cannot end movement on a trade outpost docking point.", "Kolonieschiffe dürfen ihren Flug nicht auf einem Andockpunkt beenden.");

    public static readonly LocalizedString TradeShipCannotColonize =
        new LocalizedString("Trade ships cannot end movement on a colony site.", "Handelsschiffe dürfen ihren Flug nicht auf einem Koloniebauplatz beenden.");

    public static readonly LocalizedString DestinationNotPassable =
        new LocalizedString("Destination is not passable.", "Zielfeld ist nicht passierbar.");

    public static readonly LocalizedString DestinationOccupied =
        new LocalizedString("Destination is already occupied by another ship.", "Zielfeld ist bereits von einem anderen Raumschiff besetzt.");

    public static readonly LocalizedString DestinationOpponentSpaceport =
        new LocalizedString("Destination is an opponent's spaceport point.", "Zielfeld ist ein Raumhafenpunkt eines Mitspielers.");

    public static readonly LocalizedString DestinationAlreadyHasColony =
        new LocalizedString("Destination already has a colony.", "Auf diesem Koloniebauplatz steht bereits eine Kolonie.");

    public static readonly LocalizedString DestinationInvalidOutpost =
        new LocalizedString("Destination is not a valid alien trade outpost.", "Zielfeld ist kein gültiger Außenposten eines fremden Volkes.");

    public static readonly LocalizedString DestinationNotReachable =
        new LocalizedString("The selected destination is not a legal reachable space point.", "Der gewählte Zielpunkt ist kein erreichbarer Raumpunkt.");

    public static readonly LocalizedString EncounterPendingMovementBlocked =
        new LocalizedString("Resolve the encounter before moving ships.", "Die Begegnung muss vor dem Bewegen von Raumschiffen abgehandelt werden.");

    public static readonly LocalizedString TradeShipFreightPodsStatus =
        new LocalizedString("Freight pods: {0}", "Frachtmodule: {0}");

    public static readonly LocalizedString ShipNoMovementRemaining =
        new LocalizedString("No movement allowance remaining this turn.", "Keine Bewegungspunkte mehr in diesem Zug.");

    public static readonly LocalizedString ShipMoveInstruction =
        new LocalizedString("Right-click destination on board to move.", "Rechtsklick auf Zielpunkt zum Bewegen.");

    public static readonly LocalizedString LimitTransportShipsReached =
        new LocalizedString("All 3 transport ships in use.", "Alle 3 Transportschiffe im Einsatz.");

    public static readonly LocalizedString LimitColoniesReached =
        new LocalizedString("All 4 colonies built or loaded on ships.", "Alle 4 Kolonien gebaut oder auf Schiffen.");

    public static readonly LocalizedString LimitSpaceportsReached =
        new LocalizedString("Spaceport limit reached (max 3).", "Raumhafen-Limit erreicht (max. 3).");

    public static readonly LocalizedString LimitTradeStationsReached =
        new LocalizedString("All 5 trade stations built or loaded on ships.", "Alle 5 Handelsstationen gebaut oder auf Schiffen.");

    public static readonly LocalizedString LimitUpgradesReached =
        new LocalizedString("Upgrade limit reached (max 6).", "Ausbau-Limit erreicht (max. 6).");

    public static readonly LocalizedString NoFreeSpaceportPoints =
        new LocalizedString("No free spaceport launch points available.", "Keine freien Raumhafenpunkte verfügbar.");

    public static readonly LocalizedString NotEnoughResources =
        new LocalizedString("Not enough resources.", "Nicht genügend Rohstoffe.");

    // --- Trading & Building Categories ---
    public static readonly LocalizedString BuildCategory =
        new LocalizedString("Build", "Bauen");

    public static readonly LocalizedString TradeCategory =
        new LocalizedString("Trade", "Handeln");

    // --- Trading UI ---
    public static readonly LocalizedString TradeButton =
        new LocalizedString("Trade...", "Handeln...");

    public static readonly LocalizedString TradeOverlayTitle =
        new LocalizedString("Trading", "Handel");

    public static readonly LocalizedString TradeWithSupplyTab =
        new LocalizedString("Supply Trade", "Vorratshandel");

    public static readonly LocalizedString TradeWithPlayersTab =
        new LocalizedString("Player Trade", "Spielerhandel");

    public static readonly LocalizedString SupplyTradeGivePrompt =
        new LocalizedString("Give matching resources:", "Gib gleiche Rohstoffe:");

    public static readonly LocalizedString SupplyTradeGoods11Active =
        new LocalizedString("★ 1:1 trade rate for Trade Goods active (1x per turn).", "★ 1:1-Tauschkurs für Handelsware aktiv (1x pro Zug).");

    public static readonly LocalizedString SupplyTradeGoods11Used =
        new LocalizedString("★ 1:1 trade rate already used this turn (standard 2:1 rate applies).", "★ 1:1-Tausch für diesen Zug bereits genutzt (regulärer Kurs 2:1).");

    public static readonly LocalizedString SupplyTradeReceivePrompt =
        new LocalizedString("Receive 1 resource of choice:", "Erhalte 1 Rohstoff nach Wahl:");

    public static readonly LocalizedString SupplyTradeSummary =
        new LocalizedString("Trade {0}x {1} for 1x {2}", "Tausche {0}x {1} gegen 1x {2}");

    public static readonly LocalizedString SupplyTradeSummaryGoods11Used =
        new LocalizedString("Trade {0}x {1} for 1x {2} (1:1 trade already used this turn)", "Tausche {0}x {1} gegen 1x {2} (1:1-Tausch für diesen Zug bereits verbraucht)");

    public static readonly LocalizedString ExecuteSupplyTradeButton =
        new LocalizedString("Execute Supply Trade", "Vorratshandel ausführen");

    public static readonly LocalizedString StatusSupplyTradeExecuted =
        new LocalizedString("{0} traded {1}x {2} with the supply for 1x {3}.", "{0} tauschte {1}x {2} mit dem Vorrat gegen 1x {3}.");

    public static readonly LocalizedString PlayerTradePartnerPrompt =
        new LocalizedString("Trade Partner:", "Handelspartner:");

    public static readonly LocalizedString PlayerTradeYouOfferPrompt =
        new LocalizedString("You Offer:", "Du bietest:");

    public static readonly LocalizedString PlayerTradeYouReceivePrompt =
        new LocalizedString("You Request:", "Du forderst:");

    public static readonly LocalizedString ExecutePlayerTradeButton =
        new LocalizedString("Execute Trade", "Handel ausführen");

    public static readonly LocalizedString StatusPlayerTradeExecuted =
        new LocalizedString("{0} traded with {1}.", "{0} hat mit {1} gehandelt.");

    public static readonly LocalizedString PlayerTradeInvalidPrompt =
        new LocalizedString("Both sides must offer at least 1 card.", "Beide Seiten müssen mindestens 1 Karte bieten.");

    public static readonly LocalizedString Close =
        new LocalizedString("Close", "Schließen");

    // --- Set-up / Founding Dialog ---
    public static readonly LocalizedString SetupPhaseHeading =
        new LocalizedString("Set-up Phase – {0}", "Gründungsphase – {0}");

    public static readonly LocalizedString SetupClickFreeColony =
        new LocalizedString("Click on an unoccupied colony site.", "Klicke auf einen freien Koloniepunkt.");

    public static readonly LocalizedString SetupClickOwnColony =
        new LocalizedString("Click on one of your three colonies.", "Klicke auf eine deiner drei Kolonien.");

    public static readonly LocalizedString SetupBtnRollDice =
        new LocalizedString("Roll two dice", "Mit zwei Würfeln würfeln");

    public static readonly LocalizedString SetupBtnFreeColonyShip =
        new LocalizedString("Free Colony Ship", "Kostenloses Kolonieschiff");

    public static readonly LocalizedString SetupBtnFreeTradeShip =
        new LocalizedString("Free Trade Ship", "Kostenloses Handelsschiff");

    public static readonly LocalizedString SetupBtnBoosterAvailable =
        new LocalizedString("Booster ({0} available)", "Antrieb ({0} verfügbar)");

    public static readonly LocalizedString SetupBtnCannonAvailable =
        new LocalizedString("Cannon (1 available)", "Bordkanone (1 verfügbar)");

    public static readonly LocalizedString SetupBtnFreightPodAvailable =
        new LocalizedString("Freight Pod (1 available)", "Frachtmodul (1 verfügbar)");

    // --- TriMap Founding Status ---
    public static readonly LocalizedString StatusRollForStartingPlayer =
        new LocalizedString("All players roll two dice in turn. Highest total determines the starting player.", "Alle Spieler würfeln reihum mit zwei Würfeln. Die höchste Summe bestimmt den Startspieler.");

    public static readonly LocalizedString StatusPlayerRolled =
        new LocalizedString("{0} rolls {1} + {2} = {3}.", "{0} würfelt {1} + {2} = {3}.");

    public static readonly LocalizedString StatusNextRoll =
        new LocalizedString(" Next to roll: {0}.", " Als Nächstes würfelt {0}.");

    public static readonly LocalizedString StatusStartingPlayerStarts =
        new LocalizedString(" {0} begins with the first colony.", " {0} beginnt mit der ersten Kolonie.");

    public static readonly LocalizedString StatusTieRollAgain =
        new LocalizedString(" Tie: Affected players roll again.", " Gleichstand: Die betroffenen Spieler würfeln erneut.");

    public static readonly LocalizedString StatusColonyPlaced =
        new LocalizedString("{0} placed Colony {1}.", "{0} hat Kolonie {1} gesetzt.");

    public static readonly LocalizedString StatusSpaceportUpgraded =
        new LocalizedString("{0} upgraded a colony to a spaceport. Choose your free starting ship.", "{0} hat eine Kolonie zum Raumhafen ausgebaut. Wähle das kostenlose Startschiff.");

    public static readonly LocalizedString StatusSelectedStartingShip =
        new LocalizedString("{0} selects the free {1}. Click one of the two spaceport sites.", "{0} wählt das kostenlose {1}. Klicke auf einen der zwei Raumhafenpunkte.");

    public static readonly LocalizedString StatusPlacedStartingShip =
        new LocalizedString("{0} launched a free {1}. Choose an available flagship upgrade.", "{0} hat ein kostenloses {1} eingesetzt. Wähle einen verfügbaren Flaggschiff-Ausbau.");

    public static readonly LocalizedString StatusRoundNextPlacement =
        new LocalizedString("Set-up Round {0}: {1} places next.", "Gründungsrunde {0}: {1} setzt als Nächstes.");

    public static readonly LocalizedString StatusRoundSpaceportPrompt =
        new LocalizedString("Set-up Round 4: {0} chooses a colony to become a spaceport.", "Gründungsrunde 4: {0} wählt eine eigene Kolonie als Raumhafen.");

    public static readonly LocalizedString StatusSpaceportChoosePrompt =
        new LocalizedString("{0}: Choose one of your colonies as a spaceport.", "{0}: Wähle eine eigene Kolonie als Raumhafen.");

    public static readonly LocalizedString StatusNextColonyPlacement =
        new LocalizedString("{0} places Colony {1} next.", "{0} setzt als Nächstes Kolonie {1}.");

    public static readonly LocalizedString StatusSetupComplete =
        new LocalizedString("Set-up phase complete. Each player receives 3 resource cards and one half Fame Medal.", "Gründungsphase abgeschlossen. Jeder Spieler erhält 3 Rohstoffkarten und eine halbe Medaille.");

    public static readonly LocalizedString DiscoveryTestInstruction =
        new LocalizedString("Test: Move ship {0} → {1}; discover system {2}.", "Test: Schiff {0} → {1} bewegen; System {2} aufdecken.");

    // --- Encounter Dialog ---
    public static readonly LocalizedString EncounterCardHeading =
        new LocalizedString("Encounter", "Begegnung");

    public static readonly LocalizedString EncounterOpponentHeading =
        new LocalizedString("Opponent", "Gegenspieler");

    public static readonly LocalizedString OpponentRoleRight =
        new LocalizedString("Player to your right", "Spieler rechts");

    public static readonly LocalizedString OpponentRoleLeft =
        new LocalizedString("Player to your left", "Spieler links");

    public static readonly LocalizedString OpponentRoleSecondRight =
        new LocalizedString("2nd player to your right", "2. Spieler rechts");

    public static readonly LocalizedString OpponentRoleSecondLeft =
        new LocalizedString("2nd player to your left", "2. Spieler links");

    public static readonly LocalizedString EncounterYourFlagship =
        new LocalizedString("Your Flagship", "Dein Flaggschiff");

    public static readonly LocalizedString EncounterOpponentFlagship =
        new LocalizedString("Opponent Flagship", "Gegner-Flaggschiff");

    public static readonly LocalizedString EncounterCardReaderAI =
        new LocalizedString(" (AI Opponent: {0})", " (KI-Gegenspieler: {0})");

    public static readonly LocalizedString EncounterCardReaderHuman =
        new LocalizedString(" (Opponent: {0})", " (Gegenspieler: {0})");

    public static readonly LocalizedString EncounterExecuteAllSteps =
        new LocalizedString("The following events occur:", "Folgende Ereignisse treten ein:");

    public static readonly LocalizedString EncounterEventExecutedButton =
        new LocalizedString("Continue", "Weiter");

    public static readonly LocalizedString EncounterPromptAIReading =
        new LocalizedString("Make your choice:", "Triff deine Entscheidung:");

    public static readonly LocalizedString EncounterPromptHumanReading =
        new LocalizedString("Make your choice:", "Triff deine Entscheidung:");

    public static readonly LocalizedString EncounterAnswerGivenButton =
        new LocalizedString("Continue", "Weiter");

    public static readonly LocalizedString EncounterReaderReadBoxInstruction =
        new LocalizedString("Select the applicable outcome:", "Wähle das zutreffende Ergebnis:");

    public static readonly LocalizedString EncounterOutcomePrefix =
        new LocalizedString("Outcome: ", "Ergebnis: ");

    public static readonly LocalizedString EncounterExecuteConsequenceInstruction =
        new LocalizedString("The consequences have been applied. Click 'Continue' to proceed.", "Die Folgen wurden angewendet. Klicke auf 'Weiter', um fortzufahren.");

    public static readonly LocalizedString EncounterConsequenceExecutedButton =
        new LocalizedString("Continue", "Weiter");

    // --- Automated Encounter UI Strings ---
    public static readonly LocalizedString EncounterDonateAmount =
        new LocalizedString("Donate {0}", "Spende {0}");

    public static readonly LocalizedString EncounterPayDemand =
        new LocalizedString("Pay ({0})", "Zahlen ({0})");

    public static readonly LocalizedString EncounterFight =
        new LocalizedString("Fight", "Kämpfen");

    public static readonly LocalizedString EncounterAcceptTrade =
        new LocalizedString("Accept Trade", "Tausch annehmen");

    public static readonly LocalizedString EncounterDeclineTrade =
        new LocalizedString("Decline Trade", "Tausch ablehnen");

    public static readonly LocalizedString EncounterAcceptRaid =
        new LocalizedString("Accept Raid", "Überfall annehmen");

    public static readonly LocalizedString EncounterDeclineRaid =
        new LocalizedString("Decline Raid", "Überfall ablehnen");

    public static readonly LocalizedString EncounterFlee =
        new LocalizedString("Attempt Escape", "Flucht versuchen");

    public static readonly LocalizedString EncounterHelp =
        new LocalizedString("Help", "Helfen");

    public static readonly LocalizedString EncounterDoNotHelp =
        new LocalizedString("Do Not Help", "Nicht helfen");

    public static readonly LocalizedString EncounterAttemptJump =
        new LocalizedString("Attempt Jump", "Sprung wagen");

    public static readonly LocalizedString EncounterDoNotJump =
        new LocalizedString("Do Not Jump", "Kein Sprung");

    public static readonly LocalizedString EncounterExecuteEvent =
        new LocalizedString("Execute Event", "Ereignis abwickeln");

    public static readonly LocalizedString EncounterCombatWon =
        new LocalizedString("Victory! Combat strength: {0} vs {1} ({2}).", "Sieg! Kampfkraft: {0} vs {1} ({2}).");

    public static readonly LocalizedString EncounterCombatLost =
        new LocalizedString("Defeat! Combat strength: {0} vs {1} ({2}).", "Niederlage! Kampfkraft: {0} vs {1} ({2}).");

    public static readonly LocalizedString EncounterSpeedWon =
        new LocalizedString("Faster or equal! Speed: {0} vs {1} ({2}).", "Gleich schnell oder schneller! Speed: {0} vs {1} ({2}).");

    public static readonly LocalizedString EncounterSpeedLost =
        new LocalizedString("Slower! Speed: {0} vs {1} ({2}).", "Langsamer! Speed: {0} vs {1} ({2}).");

    public static readonly LocalizedString EncounterEscapeWon =
        new LocalizedString("Escape succeeded! Speed: {0} vs {1} ({2}).", "Flucht gelungen! Geschwindigkeit: {0} vs {1} ({2}).");

    public static readonly LocalizedString EncounterEscapeFailed =
        new LocalizedString("Escape failed! Speed: {0} vs {1} ({2}). You must fight!", "Flucht misslungen! Geschwindigkeit: {0} vs {1} ({2}). Du musst kämpfen!");

    public static readonly LocalizedString EncounterRollResult =
        new LocalizedString("Flagship roll: {0} (Cores: {1}, {2}).", "Flaggschiff-Wurf: {0} (Kerne: {1}, {2}).");

    public static readonly LocalizedString EncounterShipBlockedToast =
        new LocalizedString("Ship at ({0},{1}) cannot fly this turn.", "Schiff bei ({0},{1}) darf diese Runde nicht fliegen.");

    public static readonly LocalizedString EncounterBlockShipTitle =
        new LocalizedString("Ground Ship", "Schiff blockieren");

    public static readonly LocalizedString EncounterBlockShipPrompt =
        new LocalizedString("Choose a ship that may not fly this turn:", "Wähle ein Schiff, das in dieser Runde nicht fliegen darf:");

    public static readonly LocalizedString EncounterSpaceJumpTitle =
        new LocalizedString("Space Jump", "Raumsprung");

    public static readonly LocalizedString EncounterSpaceJumpPrompt =
        new LocalizedString("Choose a ship to perform a space jump:", "Wähle ein Schiff für den Raumsprung:");

    public static readonly LocalizedString SpaceJumpTargetPrompt =
        new LocalizedString(
            "Space Jump active: Choose a destination in space for your ship (click a free tile).",
            "Raumsprung aktiv: Wähle ein Ziel im All für dein Schiff (Klick auf ein freies Feld).");

    public static readonly LocalizedString CancelSpaceJumpButton =
        new LocalizedString(
            "Cancel Space Jump",
            "Raumsprung abbrechen");

    public static readonly LocalizedString ChangeSpaceJumpShipButton =
        new LocalizedString(
            "Change Ship",
            "Anderes Schiff wählen");

    public static readonly LocalizedString SpaceJumpSuccessToast =
        new LocalizedString(
            "Space jump successful! Ship relocated to ({0}, {1}).",
            "Raumsprung erfolgreich! Schiff nach ({0}, {1}) versetzt.");

    public static readonly LocalizedString SpaceJumpCancelledToast =
        new LocalizedString(
            "Space jump cancelled. Ship remains at its current position.",
            "Raumsprung abgebrochen. Das Schiff bleibt an seiner Position.");

    public static readonly LocalizedString SpaceJumpSamePositionError =
        new LocalizedString(
            "The ship is already at this location.",
            "Das Schiff befindet sich bereits an diesem Ort.");

    public static readonly LocalizedString EncounterChooseResourceRewardPrompt =
        new LocalizedString("Choose {0} resource(s) of your choice:", "Wähle {0} Rohstoff(e) deiner Wahl:");

    public static readonly LocalizedString EncounterSelectedRewardCount =
        new LocalizedString("Selected: {0} / {1}", "Ausgewählt: {0} / {1}");

    public static readonly LocalizedString EncounterConfirmReward =
        new LocalizedString("Confirm Selection ({0}/{1})", "Auswahl bestätigen ({0}/{1})");

    public static readonly LocalizedString EncounterRewardReceivedToast =
        new LocalizedString("You received {0} resource(s) of your choice.", "Du hast {0} Rohstoff(e) deiner Wahl erhalten.");

    // --- Interactive Rolling UI Strings ---
    public static readonly LocalizedString RollProductionDiceButton =
        new LocalizedString("Roll Production Dice (2d6)", "Ertrag auswürfeln (2W6)");

    public static readonly LocalizedString ProductionDiceRollingPrompt =
        new LocalizedString("Production Phase: Click button to roll 2 dice for resources.", "Ertragsphase: Klicke auf den Button, um Ertrag auszuwürfeln.");

    public static readonly LocalizedString EngageFlightDrivesButton =
        new LocalizedString("Engage Flight Drives", "Flugantrieb zünden");

    public static readonly LocalizedString FlightRollPendingPrompt =
        new LocalizedString("Flight Phase: Engage flight drives to determine ship speed & encounters.", "Flugphase: Zünde den Flugantrieb für Geschwindigkeit & Begegnungen.");

    public static readonly LocalizedString CycleMovableShipsButton =
        new LocalizedString("Next Movable Ship ({0}) [Tab]", "Nächstes Schiff mit Bewegung ({0}) [Tab]");

    public static readonly LocalizedString CapturedShipDeployedToast =
        new LocalizedString("Trade Ship captured! Deployed at spaceport at ({0},{1}).", "Handelsschiff erbeutet! Es wurde an deinem Raumhafen bei ({0},{1}) aufgestellt.");

    public static readonly LocalizedString CapturedShipStoredToast =
        new LocalizedString("Trade Ship captured! Added to reserves ({0}) - deploy for free in build phase.", "Handelsschiff erbeutet! Es wurde deiner Reserve hinzugefügt ({0}) – kann in der Bauphase kostenlos aufgestellt werden.");

    public static readonly LocalizedString CapturedShipStoredMaxShipsReason =
        new LocalizedString("3 ships already active", "Bereits 3 Schiffe aktiv");

    public static readonly LocalizedString CapturedShipStoredNoSpaceportReason =
        new LocalizedString("no free spaceport point", "Kein freier Raumhafenpunkt");

    public static readonly LocalizedString DeployCapturedTradeShipButton =
        new LocalizedString("Deploy Captured Trade Ship ({0}) [Free]", "Erbeutetes Handelsschiff aufstellen ({0}) [Kostenlos]");

    public static readonly LocalizedString DeployCapturedShipPlacingInstruction =
        new LocalizedString("Place your captured Trade Ship on a free spaceport point (Free).", "Platziere dein erbeutetes Handelsschiff auf einem freien Raumhafenpunkt (Kostenlos).");

    public static readonly LocalizedString CapturedTradeShipsLabel =
        new LocalizedString("Captured Trade Ships", "Erbeutete Handelsschiffe");

    public static readonly LocalizedString EncounterShakeCombatButton =
        new LocalizedString("Fire Cannons (Combat Roll)", "Bordkanonen abfeuern (Kampftest)");

    public static readonly LocalizedString EncounterShakeSpeedButton =
        new LocalizedString("Boost Thrusters (Speed Check)", "Schubdüsen zünden (Geschwindigkeitstest)");

    public static readonly LocalizedString EncounterShakeTradeButton =
        new LocalizedString("Scan Corsair Response (Sensor Check)", "Korsarensignal scannen (Sensortest)");

    public static readonly LocalizedString EncounterDonationSelectionPrompt =
        new LocalizedString("Select {0} resource(s) from your stock to donate:", "Wähle {0} Rohstoff(e) aus deinem Vorrat zum Spenden:");

    public static readonly LocalizedString EncounterConfirmDonation =
        new LocalizedString("Confirm Donation ({0}/{1})", "Spende bestätigen ({0}/{1})");

    public static readonly LocalizedString EncounterSelectedDonationCount =
        new LocalizedString("Selected: {0} of {1}", "Ausgewählt: {0} von {1}");

    public static readonly LocalizedString AiTurnInProgress =
        new LocalizedString("{0} (AI) is thinking...", "{0} (KI) ist am Zug...");

    public static readonly LocalizedString PlayerTurnInProgress =
        new LocalizedString("{0}'s turn...", "{0} ist am Zug...");

    public static readonly LocalizedString PlayerTurnWithPhase =
        new LocalizedString("{0}'s turn ({1})...", "{0} ist am Zug ({1})...");

    public static readonly LocalizedString ToggleBasic =
        new LocalizedString("▲ Basic", "▲ Basis");

    public static readonly LocalizedString ToggleMore =
        new LocalizedString("▼ More", "▼ Mehr");

    // --- Victory Points Overview ---
    public static readonly LocalizedString VictoryPointsHeading =
        new LocalizedString("Victory Points", "Siegpunkte");

    public static readonly LocalizedString VictoryPointsOverviewTitle =
        new LocalizedString("Victory Points Overview", "Siegpunktübersicht");

    public static readonly LocalizedString VictoryPointsDetailsFormat =
        new LocalizedString("Colonies: {0} | Spaceports: {1} | Medals: {2} | Friends: {3}",
                            "Kolonien: {0} | Raumhäfen: {1} | Medaillen: {2} | Freundschaft: {3}");

    // --- Game Over ---
    public static readonly LocalizedString GameOverTitle =
        new LocalizedString("Game Over", "Spielende");

    public static readonly LocalizedString GameOverWinnerFormat =
        new LocalizedString("{0} wins with {1} victory points!",
                            "{0} gewinnt mit {1} Siegpunkten!");

    public static readonly LocalizedString GameOverStandingsLine =
        new LocalizedString("{0}. {1} - {2} VP", "{0}. {1} - {2} Siegpunkte");

    public static readonly LocalizedString GameOverNewGame =
        new LocalizedString("New Game", "Neues Spiel");

    public static readonly LocalizedString GameOverViewBoard =
        new LocalizedString("View Board", "Spielfeld ansehen");

    // --- Building Costs Tooltip & Overview ---
    public static readonly LocalizedString BuildingCostHeader =
        new LocalizedString("Costs:", "Kosten:");

    public static readonly LocalizedString BuildingCostsOverviewTitle =
        new LocalizedString("Building Costs", "Baukosten");

    public static string FormatCost(IDictionary<ResourceType, int> cost)
    {
        if (cost == null || cost.Count == 0) return string.Empty;
        var parts = new List<string>();
        foreach (var kvp in cost)
        {
            if (kvp.Value > 0)
            {
                parts.Add(kvp.Value + " " + GetResourceName(kvp.Key));
            }
        }
        return string.Join(", ", parts);
    }

    // --- Alien Civilizations & Outpost Benefits ---
    public static readonly LocalizedString CivMerchantsName =
        new LocalizedString("The Consortium of Elyron", "Das Konsortium von Elyron");
    public static readonly LocalizedString CivMerchantsShortName =
        new LocalizedString("The Elyrians", "Die Elyrier");
    public static readonly LocalizedString CivMerchantsBenefit =
        new LocalizedString("Trade Advantage (The Elyrians): Trade resources at 2:1 and goods at 1:1 with the bank.",
                            "Handelsvorteil (Die Elyrier): Rohstoffe 2:1 und Handelswaren 1:1 mit dem Vorrat tauschen.");

    public static readonly LocalizedString CivScholarsName =
        new LocalizedString("The Synapse of Xylos", "Die Synapse von Xylos");
    public static readonly LocalizedString CivScholarsShortName =
        new LocalizedString("The Xylos Scholars", "Die Xylos-Gelehrten");
    public static readonly LocalizedString CivScholarsBenefit =
        new LocalizedString("Tech Advantage (Xylos Scholars): Increases ship flight speed (+Booster) and cannon combat strength.",
                            "Technologie (Xylos-Gelehrte): Erhöht Fluggeschwindigkeit (+Antrieb) und Kanonen-Kampfkraft.");

    public static readonly LocalizedString CivDiplomatsName =
        new LocalizedString("The Conclave of Aurelia", "Das Konklave von Aurelia");
    public static readonly LocalizedString CivDiplomatsShortName =
        new LocalizedString("The Aurelians", "Die Aurelianer");
    public static readonly LocalizedString CivDiplomatsBenefit =
        new LocalizedString("Diplomacy (The Aurelians): Tribute discount on rolled 7 (lose cards only at >12), buy fame medals.",
                            "Diplomatie (Die Aurelianer): Tribut-Ermäßigung bei 7 (Abgabe erst ab >12 Karten), Ruhm-Medaillen kaufen.");

    public static readonly LocalizedString CivCultivatorsName =
        new LocalizedString("The Terra-Symbionts of Veda", "Die Terra-Symbionten von Veda");
    public static readonly LocalizedString CivCultivatorsShortName =
        new LocalizedString("The Vedans", "Die Vedaner");
    public static readonly LocalizedString CivCultivatorsBenefit =
        new LocalizedString("Biosphere Bonus (The Vedans): +1 additional resource on matching production rolls.",
                            "Biosphären-Bonus (Die Vedaner): +1 zusätzlicher Rohstoff bei passendem Ertragswurf.");

    public static readonly LocalizedString OutpostFriendshipBonus =
        new LocalizedString("Trade Station: Grants 1 Friendship Card upon building, and 2 Victory Points for station majority.",
                            "Handelsstation: Bringt beim Bau 1 Freundschaftskarte und 2 Siegpunkte bei Stations-Mehrheit.");

    public static string GetAlienCivilizationName(AlienCivilization civ)
    {
        switch (civ)
        {
            case AlienCivilization.Trader: return CivMerchantsName.GetText();
            case AlienCivilization.Scientist: return CivScholarsName.GetText();
            case AlienCivilization.Diplomat: return CivDiplomatsName.GetText();
            case AlienCivilization.Cultivator: return CivCultivatorsName.GetText();
            default: return string.Empty;
        }
    }

    public static string GetAlienCivilizationShortName(AlienCivilization civ)
    {
        switch (civ)
        {
            case AlienCivilization.Trader: return CivMerchantsShortName.GetText();
            case AlienCivilization.Scientist: return CivScholarsShortName.GetText();
            case AlienCivilization.Diplomat: return CivDiplomatsShortName.GetText();
            case AlienCivilization.Cultivator: return CivCultivatorsShortName.GetText();
            default: return string.Empty;
        }
    }

    public static string GetAlienCivilizationBenefit(AlienCivilization civ)
    {
        switch (civ)
        {
            case AlienCivilization.Trader: return CivMerchantsBenefit.GetText();
            case AlienCivilization.Scientist: return CivScholarsBenefit.GetText();
            case AlienCivilization.Diplomat: return CivDiplomatsBenefit.GetText();
            case AlienCivilization.Cultivator: return CivCultivatorsBenefit.GetText();
            default: return string.Empty;
        }
    }

    // --- Friendship Cards & Standee Localization ---
    public static readonly LocalizedString FriendshipStandeeAcquired =
        new LocalizedString("{0} received the Friendship Standee for {1} (+2 Victory Points)!",
                            "{0} erhält den Freundschafts-Aufsteller für {1} (+2 Siegpunkte)!");
    public static readonly LocalizedString FriendshipStandeeLost =
        new LocalizedString("{0} lost the Friendship Standee for {1} to {2} (-2 Victory Points)!",
                            "{0} verliert den Freundschafts-Aufsteller für {1} an {2} (-2 Siegpunkte)!");
    public static readonly LocalizedString ChooseFriendshipCardTitle =
        new LocalizedString("CHOOSE A FRIENDSHIP CARD", "WÄHLE EINE FREUNDSCHAFTSKARTE");
    public static readonly LocalizedString ChooseFriendshipCardPrompt =
        new LocalizedString("You founded a trade station with {0}. Choose 1 card from their deck:",
                            "Du hast eine Handelsstation bei {0} gegründet. Wähle 1 Karte aus dem Vorrat:");
    public static readonly LocalizedString FriendshipCardsHeading =
        new LocalizedString("Friendship Cards", "Freundschaftskarten");
    public static readonly LocalizedString FriendshipStandeesHeading =
        new LocalizedString("Friendship Markers", "Freundschafts-Aufsteller");
    public static readonly LocalizedString BuyFameButton =
        new LocalizedString("Buy Medal (1 Good)", "Medaille kaufen (1 Ware)");
    public static readonly LocalizedString RichHelpsPoorButton =
        new LocalizedString("Rich Helps Poor", "Reich hilft Arm");
    public static readonly LocalizedString GalacticAidNotification =
        new LocalizedString("Galactic Aid: You received 1 {0} because you had no production.",
                            "Galaktische Hilfe: Du hast 1 {0} erhalten, da du keine Erträge hattest.");
    public static readonly LocalizedString ConfirmEndTurnTitle =
        new LocalizedString("END TURN?", "ZUG BEENDEN?");
    public static readonly LocalizedString ConfirmEndTurnPrompt =
        new LocalizedString("You still have {0} ship(s) with remaining movement points ({1} total).\nAre you sure you want to end your turn?",
                            "Du hast noch {0} Schiff(e) mit verbleibenden Bewegungspunkten ({1} insgesamt).\nMöchtest du deinen Zug wirklich beenden?");
    public static readonly LocalizedString ConfirmEndTurnYes =
        new LocalizedString("End Turn", "Zug beenden");
    public static readonly LocalizedString ConfirmEndTurnNo =
        new LocalizedString("Cancel / Keep Moving", "Abbrechen / Weiterfliegen");

    // --- 7-Roll Resolution Strings ---
    public static readonly LocalizedString SevenRollTitle =
        new LocalizedString("7 ROLLED!", "7 GEWÜRFELT!");

    public static readonly LocalizedString SevenRollDiscardTitle =
        new LocalizedString("DISCARD HALF CARDS", "HÄLFTE DER KARTEN ABGEBEN");

    public static readonly LocalizedString SevenRollDiscardPrompt =
        new LocalizedString("{0}, you hold {1} cards (limit: {2}). You must choose and discard {3} card(s):",
                            "{0}, du besitzt {1} Karten (Limit: {2}). Du musst {3} Karte(n) abgeben:");

    public static readonly LocalizedString SevenRollDiscardCount =
        new LocalizedString("Selected: {0} / {1}", "Ausgewählt: {0} / {1}");

    public static readonly LocalizedString SevenRollDiscardConfirm =
        new LocalizedString("Confirm Discard", "Abgabe bestätigen");

    public static readonly LocalizedString SevenRollDiscardToast =
        new LocalizedString("{0} discarded {1} resource card(s).", "{0} hat {1} Rohstoffkarte(n) abgegeben.");

    public static readonly LocalizedString SevenRollStealTitle =
        new LocalizedString("STEAL A RESOURCE CARD", "ROHSTOFFKARTE STEHLEN");

    public static readonly LocalizedString SevenRollStealPrompt =
        new LocalizedString("Choose an opponent with resource cards to draw 1 card from their hand:",
                            "Wähle einen Mitspieler, um 1 verdeckte Rohstoffkarte aus seiner Hand zu ziehen:");

    public static readonly LocalizedString SevenRollStealNoTargets =
        new LocalizedString("No opponent has resource cards to steal.",
                            "Kein Mitspieler besitzt Rohstoffkarten zum Stehlen.");

    public static readonly LocalizedString SevenRollStealSuccessToast =
        new LocalizedString("{0} stole 1 {1} from {2}!", "{0} hat 1 {1} von {2} gestohlen!");

    public static readonly LocalizedString SevenRollOpponentsReserveToast =
        new LocalizedString("Opponents draw 1 card from Reserve Pile: {0}",
                            "Mitspieler ziehen je 1 Karte vom Vorratsstapel: {0}");

    public static readonly LocalizedString SevenRollActiveReserveToast =
        new LocalizedString("{0} draws {1} card(s) from Reserve Pile ({2} Victory Points).",
                            "{0} zieht {1} Karte(n) vom Vorratsstapel ({2} Siegpunkte).");

    public static readonly LocalizedString SevenRollHandCardsCount =
        new LocalizedString("{0} card(s)", "{0} Karte(n)");

    // --- Encounter Surrender Strings ---
    public static readonly LocalizedString EncounterSurrenderTitle =
        new LocalizedString("HAND OVER RESOURCES", "ROHSTOFFE ABGEBEN");

    public static readonly LocalizedString EncounterSurrenderPrompt =
        new LocalizedString("{0}, please choose {1} resource card(s) to hand over:",
                            "{0}, bitte wähle {1} Rohstoffkarte(n) zum Abgeben:");

    public static readonly LocalizedString EncounterMarauderDemandPrompt =
        new LocalizedString("{0}, select {1} resource card(s) to give to the Void Corsairs:",
                            "{0}, wähle {1} Rohstoffkarte(n) für die Void-Korsaren:");

    public static readonly LocalizedString EncounterSurrenderToast =
        new LocalizedString("{0} handed over {1} resource card(s).",
                            "{0} hat {1} Rohstoffkarte(n) abgegeben.");

    // --- Encounter: Drawing / Stealing from opponents ---
    public static readonly LocalizedString EncounterStealFromPlayerEntry =
        new LocalizedString("{0} from {1}", "{0} von {1}");

    public static readonly LocalizedString EncounterStealAllSuccessToast =
        new LocalizedString("Drawn from opponents: {0}.",
                            "Von Mitspielern gezogen: {0}.");

    public static readonly LocalizedString EncounterStealAllPartialToast =
        new LocalizedString("Drawn from opponents: {0} ({1} had no resource cards).",
                            "Von Mitspielern gezogen: {0} ({1} ohne Rohstoffe).");

    public static readonly LocalizedString EncounterStealAllNoneToast =
        new LocalizedString("No cards drawn: Opponents had no resource cards in hand.",
                            "Keine Karten gezogen: Die Mitspieler hatten keine Rohstoffe auf der Hand.");

    public static readonly LocalizedString DiplomatRichHelpsPoorToast =
        new LocalizedString("Rich Helps Poor: Received {0}!",
                            "Reich hilft Arm: {0} erhalten!");

    // --- Encounter: lose a flagship upgrade ---
    public static readonly LocalizedString EncounterLoseUpgradeTitle =
        new LocalizedString("REMOVE UPGRADE", "AUSBAU ENTFERNEN");

    public static readonly LocalizedString EncounterLoseUpgradePrompt =
        new LocalizedString("{0}, choose which flagship upgrade to remove:",
                            "{0}, wähle den Ausbau, der vom Flaggschiff entfernt wird:");

    public static readonly LocalizedString EncounterLoseUpgradeOption =
        new LocalizedString("{0} (you have {1})", "{0} (vorhanden: {1})");

    public static readonly LocalizedString EncounterLoseUpgradeToast =
        new LocalizedString("{0} lost a flagship upgrade: {1}.",
                            "{0} hat einen Flaggschiff-Ausbau verloren: {1}.");

    // --- Encounter: gain a flagship upgrade ---
    public static readonly LocalizedString EncounterGainUpgradeTitle =
        new LocalizedString("CHOOSE UPGRADE", "AUSBAU WÄHLEN");

    public static readonly LocalizedString EncounterGainUpgradePrompt =
        new LocalizedString("{0}, choose which flagship upgrade to mount:",
                            "{0}, wähle den Ausbau, der auf dein Flaggschiff montiert wird:");

    public static readonly LocalizedString EncounterGainUpgradeOption =
        new LocalizedString("Mount {0} ({1} in supply)", "{0} montieren ({1} im Vorrat)");

    public static readonly LocalizedString EncounterGainUpgradeToast =
        new LocalizedString("{0} mounted a flagship upgrade: {1}.",
                            "{0} hat einen Flaggschiff-Ausbau montiert: {1}.");

    // --- Special Planet Types & Hazards ---
    public static readonly LocalizedString IcePlanet =
        new LocalizedString("Ice Planet", "Eisplanet");
    public static readonly LocalizedString MarauderBase =
        new LocalizedString("Corsair Base", "Korsarenbasis");
    public static readonly LocalizedString SpecialTokensHeading =
        new LocalizedString("Special Tokens", "Sondermarker");
    public static readonly LocalizedString CannotColonizeIcePlanet =
        new LocalizedString("Colony site blocked: Adjacent ice planet must be terraformed first ({0} freight pods needed).",
                            "Koloniebau blockiert: Der angrenzende Eisplanet muss zuerst terraformiert werden ({0} Frachtmodule nötig).");
    public static readonly LocalizedString CannotColonizeMarauderBase =
        new LocalizedString("Colony site blocked: Adjacent Void Corsair base must be defeated first ({0} cannons needed).",
                            "Koloniebau blockiert: Die angrenzende Void-Korsaren-Basis muss zuerst besiegt werden ({0} Bordkanonen nötig).");
    public static readonly LocalizedString TerraformSuccessToast =
        new LocalizedString("{0} terraformed an Ice Planet! (+1 Victory Point, new production number: {1})",
                            "{0} hat einen Eisplaneten terraformiert! (+1 Siegpunkt, neue Zahl: {1})");
    public static readonly LocalizedString MarauderBaseDefeatedToast =
        new LocalizedString("{0} defeated a Void Corsair Base! (+1 Victory Point, new production number: {1})",
                            "{0} hat eine Void-Korsaren-Basis besiegt! (+1 Siegpunkt, neue Zahl: {1})");

    // --- Planet Tooltips & Sci-Fi Displays ---
    public static readonly LocalizedString PlanetTooltipSystemTitle =
        new LocalizedString("STAR SYSTEM", "STERNENSYSTEM");
    public static readonly LocalizedString PlanetTooltipHazardZone =
        new LocalizedString("HAZARD ZONE", "GEFAHRENZONE");
    public static readonly LocalizedString PlanetTooltipSecureZone =
        new LocalizedString("SECURE SYSTEM", "SICHERES SYSTEM");
    public static readonly LocalizedString PlanetTooltipUndiscovered =
        new LocalizedString("Undiscovered (fly close to scan)", "Unentdeckt (heranfliegen zum Scannen)");
    public static readonly LocalizedString PlanetTooltipRollFormat =
        new LocalizedString("Roll {0} · {1} pips ({2}% chance)", "Zahl {0} · {1} Punkte ({2}% Chance)");
    public static readonly LocalizedString PlanetTooltipIceHazard =
        new LocalizedString("ICE PLANET: Requires {0} Freight Pods to terraform", "EISPLANET: Benötigt {0} Frachtmodule zum Terraformen");
    public static readonly LocalizedString PlanetTooltipMarauderHazard =
        new LocalizedString("VOID CORSAIR BASE: Requires {0} Cannons to defeat", "VOID-KORSAREN-BASIS: Benötigt {0} Bordkanonen zum Besiegen");
    public static readonly LocalizedString PlanetTooltipFlagshipStatus =
        new LocalizedString("Flagship: {0}/{1} ({2})", "Flaggschiff: {0}/{1} ({2})");
    public static readonly LocalizedString PlanetTooltipReady =
        new LocalizedString("Ready to colonize", "Bereit zum Kolonisieren");
    public static readonly LocalizedString PlanetTooltipNotReady =
        new LocalizedString("Upgrade needed", "Ausbau erforderlich");
    public static readonly LocalizedString PlanetTooltipHazardReward =
        new LocalizedString("Reward: +1 Victory Point & production number chip", "Belohnung: +1 Siegpunkt & Ertragsplättchen");
    public static readonly LocalizedString PlanetTooltipSpecialResolved =
        new LocalizedString("Danger cleared (+1 Victory Point awarded)", "Gefahr gebannt (+1 Siegpunkt erhalten)");

    // --- Game Setup Start Screen ---
    public static readonly LocalizedString GameSetupTitle =
        new LocalizedString("★ DEEP SPACE PIONEERS ★", "★ DEEP SPACE PIONEERS ★");
    public static readonly LocalizedString GameSetupSubtitle =
        new LocalizedString("Game Setup & Mission Briefing", "Spielkonfiguration & Missionsvorbereitung");
    public static readonly LocalizedString GameSetupPlayersHeading =
        new LocalizedString("PLAYERS & AI DIFFICULTY", "SPIELER & KI-SCHWIERIGKEITSGRADE");
    public static readonly LocalizedString GameSetupSeedHeading =
        new LocalizedString("UNIVERSE SEED & MAP RANDOMIZATION", "UNIVERSUM-SEED & ZUFALLSKARTE");
    public static readonly LocalizedString GameSetupStartButton =
        new LocalizedString("🚀 LAUNCH MISSION", "🚀 SPIEL STARTEN");
    public static readonly LocalizedString GameSetupResetButton =
        new LocalizedString("Reset Defaults", "Standard wiederherstellen");
    public static readonly LocalizedString GameSetupPlayerLocal =
        new LocalizedString("👤 Human", "👤 Mensch");
    public static readonly LocalizedString GameSetupPlayerAi =
        new LocalizedString("🤖 AI", "🤖 KI");
    public static readonly LocalizedString GameSetupDifficultyEasy =
        new LocalizedString("Easy", "Einfach");
    public static readonly LocalizedString GameSetupDifficultyNormal =
        new LocalizedString("Normal", "Normal");
    public static readonly LocalizedString GameSetupDifficultyHard =
        new LocalizedString("Hard", "Schwer");
    public static readonly LocalizedString GameSetupSeedRandom =
        new LocalizedString("🎲 Random Universe", "🎲 Zufälliges Universum");
    public static readonly LocalizedString GameSetupSeedFixed =
        new LocalizedString("🔒 Fixed Seed", "🔒 Feste Vorgabe");
    public static readonly LocalizedString GameSetupRerollSeed =
        new LocalizedString("🎲 Reroll", "🎲 Neu würfeln");
    public static readonly LocalizedString GameSetupSeedPlaceholder =
        new LocalizedString("Enter seed...", "Seed eingeben...");
    public static readonly LocalizedString GameSetupSeedAuto =
        new LocalizedString("Random (auto)", "Zufällig (automatisch)");
    public static readonly LocalizedString GameSetupDebugLabelsOn =
        new LocalizedString("🏷️ Quadrant Markers: ON", "🏷️ Quadranten-Marker: AN");
    public static readonly LocalizedString GameSetupDebugLabelsOff =
        new LocalizedString("🏷️ Quadrant Markers: OFF", "🏷️ Quadranten-Marker: AUS");

    // --- AI Takeover Inactivity Warning ---
    public static readonly LocalizedString AiTakeoverTitle =
        new LocalizedString("⚠️ Inactivity Warning", "⚠️ Inaktivitäts-Warnung");
    public static readonly LocalizedString AiTakeoverNotice =
        new LocalizedString("You have not reacted for 90 seconds.", "Du hast seit 90 Sekunden nicht reagiert.");
    public static readonly LocalizedString AiTakeoverCountdownFormat =
        new LocalizedString("AI takes over your turn in {0}s", "KI übernimmt deinen Zug in {0}s");
    public static readonly LocalizedString AiTakeoverCancelButton =
        new LocalizedString("I'm still here! (Cancel)", "Ich bin noch da! (Abbrechen)");

    // --- Language Switcher ---
    public static readonly LocalizedString LanguageButton =
        new LocalizedString("🌐 Language: English", "🌐 Sprache: Deutsch");
    public static readonly LocalizedString LanguageShortButton =
        new LocalizedString("🌐 EN", "🌐 DE");
    public static readonly LocalizedString GameSetupTabLocal =
        new LocalizedString("👥 Local Game", "👥 Lokales Spiel");
    public static readonly LocalizedString GameSetupTabBrowser =
        new LocalizedString("🌐 Find Server (LAN)", "🌐 Server suchen (LAN)");
    public static readonly LocalizedString GameSetupTabHost =
        new LocalizedString("🚀 Host LAN", "🚀 LAN hosten");
}


