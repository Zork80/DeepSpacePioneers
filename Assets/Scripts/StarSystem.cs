using System;
using UnityEngine;

public class StarSystem : MonoBehaviour
{
    // Sci-Fi Holo Colors
    private static readonly Color HoloDiscBaseColor = new Color(0.04f, 0.08f, 0.14f, 0.95f);
    private static readonly Color HoloBorderNormal = new Color(0.18f, 0.72f, 0.95f, 1f);     // Neon Cyan
    private static readonly Color HoloBorderHot = new Color(1.0f, 0.32f, 0.18f, 1f);        // Hot Orange/Red for 6 & 8
    private static readonly Color HoloBorderSilver = new Color(0.75f, 0.85f, 0.95f, 1f);     // Silver for 2 & 12
    private static readonly Color HoloBorderIce = new Color(0.1f, 0.92f, 1.0f, 1f);         // Glacial Cyan
    private static readonly Color HoloBorderMarauder = new Color(1.0f, 0.18f, 0.22f, 1f);    // Warning Crimson
    private static readonly Color HoloBorderHidden = new Color(0.55f, 0.45f, 0.95f, 1f);     // Sensor Purple
    private static readonly Color HoloBorderResolved = new Color(1.0f, 0.82f, 0.2f, 1f);     // Golden Victory

    private readonly TextMesh[] productionNumberLabels = new TextMesh[3];
    private readonly TextMesh[] productionNumberSubLabels = new TextMesh[3];

    private readonly PlanetSpecialType[] specialTypes = new PlanetSpecialType[3];
    private readonly int[] specialRequirements = new int[3];
    private readonly bool[] specialResolved = new bool[] { true, true, true };
    private bool areNumbersRevealed;
    public bool AreNumbersRevealed => areNumbersRevealed;

    Material _planet1Material;
    public Material Planet1Material 
    {
         get => _planet1Material;
         set
         {
            _planet1Material = value;
            var planet1 = GetChildByName("Planet1");
            planet1.GetComponent<MeshRenderer> ().material = Planet1Material;
         }
    }
    Material _planet2Material;
    public Material Planet2Material
    {
         get { return _planet2Material; }
         set
         {
            _planet2Material = value;
            var planet2 = GetChildByName("Planet2");
            planet2.GetComponent<MeshRenderer> ().material = Planet2Material;
         }
    }

    Material _planet3Material;
    public Material Planet3Material
    {
         get { return _planet3Material; }
         set
         {
            _planet3Material = value;
            var planet3 = GetChildByName("Planet3");
            planet3.GetComponent<MeshRenderer> ().material = Planet3Material;
         }
    }

    public void SetPlanetProductionNumbers(
        int planet1,
        int planet2,
        int planet3,
        bool revealed)
    {
        areNumbersRevealed = revealed;
        int[] numbers = { planet1, planet2, planet3 };
        string[] planetNames = { "Planet1", "Planet2", "Planet3" };
        for (int i = 0; i < numbers.Length; i++)
        {
            if (numbers[i] < 2 || numbers[i] > 12 || numbers[i] == 7)
            {
                throw new ArgumentOutOfRangeException("numbers");
            }

            Transform planet = GetChildByName(planetNames[i]);
            if (planet == null)
            {
                throw new InvalidOperationException(
                    "The star-system prefab is missing " + planetNames[i] + ".");
            }

            CreateProductionNumberChip(planet, numbers[i], i, revealed);
        }
        UpdateChipsDisplay();
    }

    public void SetPlanetSpecialTypes(
        PlanetSpecialType type1, int req1,
        PlanetSpecialType type2, int req2,
        PlanetSpecialType type3, int req3)
    {
        specialTypes[0] = type1; specialRequirements[0] = req1; specialResolved[0] = (type1 == PlanetSpecialType.Normal);
        specialTypes[1] = type2; specialRequirements[1] = req2; specialResolved[1] = (type2 == PlanetSpecialType.Normal);
        specialTypes[2] = type3; specialRequirements[2] = req3; specialResolved[2] = (type3 == PlanetSpecialType.Normal);
        UpdateChipsDisplay();
    }

    public void UpdatePlanetSpecialState(int index, PlanetSpecialType type, int requirement, bool resolved, int productionNumber)
    {
        if (index < 0 || index >= 3) return;
        specialTypes[index] = type;
        specialRequirements[index] = requirement;
        specialResolved[index] = resolved;
        if (productionNumberLabels[index] != null)
        {
            productionNumberLabels[index].gameObject.name = productionNumber.ToString();
        }
        UpdateChipsDisplay();
    }

    public void RevealPlanetProductionNumbers()
    {
        areNumbersRevealed = true;
        UpdateChipsDisplay();
    }

    public void UpdateChipsDisplay()
    {
        for (int i = 0; i < productionNumberLabels.Length; i++)
        {
            if (productionNumberLabels[i] == null)
            {
                continue;
            }

            TextMesh mainLabel = productionNumberLabels[i];
            TextMesh subLabel = productionNumberSubLabels[i];

            if (!areNumbersRevealed)
            {
                mainLabel.text = "?";
                mainLabel.characterSize = 0.09f;
                mainLabel.transform.localPosition = new Vector3(0f, 0.032f, 0f);
                mainLabel.color = HoloBorderHidden;
                if (subLabel != null)
                {
                    subLabel.text = "[SCAN]";
                    subLabel.characterSize = 0.038f;
                    subLabel.transform.localPosition = new Vector3(0f, -0.045f, 0f);
                    subLabel.color = new Color(0.7f, 0.65f, 1f, 0.85f);
                }
                continue;
            }

            bool isGerman = LocalizationManager.CurrentLanguage == GameLanguage.German;

            if (specialTypes[i] == PlanetSpecialType.IcePlanet && !specialResolved[i])
            {
                mainLabel.text = isGerman ? "EIS" : "ICE";
                mainLabel.characterSize = 0.042f;
                mainLabel.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                mainLabel.color = HoloBorderIce;
                if (subLabel != null)
                {
                    subLabel.text = isGerman ? (specialRequirements[i] + " FRACHT") : (specialRequirements[i] + " PODS");
                    subLabel.characterSize = 0.028f;
                    subLabel.transform.localPosition = new Vector3(0f, -0.042f, 0f);
                    subLabel.color = new Color(0.65f, 0.95f, 1f, 0.9f);
                }
            }
            else if (specialTypes[i] == PlanetSpecialType.MarauderBase && !specialResolved[i])
            {
                mainLabel.text = isGerman ? "KORSAR" : "CORSAIR";
                mainLabel.characterSize = 0.038f;
                mainLabel.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                mainLabel.color = HoloBorderMarauder;
                if (subLabel != null)
                {
                    subLabel.text = isGerman ? (specialRequirements[i] + " KANONEN") : (specialRequirements[i] + " GUNS");
                    subLabel.characterSize = 0.026f;
                    subLabel.transform.localPosition = new Vector3(0f, -0.042f, 0f);
                    subLabel.color = new Color(1.0f, 0.65f, 0.7f, 0.9f);
                }
            }
            else
            {
                int.TryParse(mainLabel.gameObject.name, out int num);
                bool wasSpecial = specialTypes[i] != PlanetSpecialType.Normal && specialResolved[i];

                Color numberColor;
                if (num == 6 || num == 8)
                {
                    numberColor = HoloBorderHot;
                }
                else if (num == 2 || num == 12)
                {
                    numberColor = HoloBorderSilver;
                }
                else
                {
                    numberColor = HoloBorderNormal;
                }

                mainLabel.text = num > 0 ? num.ToString() : mainLabel.gameObject.name;
                mainLabel.color = numberColor;

                if (wasSpecial)
                {
                    mainLabel.characterSize = num >= 10 ? 0.075f : 0.085f;
                    mainLabel.transform.localPosition = new Vector3(0f, 0.030f, 0f);
                    if (subLabel != null)
                    {
                        subLabel.text = "\u2605 +1 VP \u2605";
                        subLabel.characterSize = 0.034f;
                        subLabel.transform.localPosition = new Vector3(0f, -0.042f, 0f);
                        subLabel.color = HoloBorderResolved;
                    }
                }
                else
                {
                    // Clean centered number without background circle or pips
                    mainLabel.characterSize = num >= 10 ? 0.080f : 0.090f;
                    mainLabel.transform.localPosition = Vector3.zero;
                    if (subLabel != null)
                    {
                        subLabel.text = string.Empty;
                    }
                }
            }
        }
    }

    private static string GetPipsString(int number)
    {
        switch (number)
        {
            case 2:
            case 12:
                return "\u2022";
            case 3:
            case 11:
                return "\u2022 \u2022";
            case 4:
            case 10:
                return "\u2022 \u2022 \u2022";
            case 5:
            case 9:
                return "\u2022 \u2022 \u2022 \u2022";
            case 6:
            case 8:
                return "\u2022 \u2022 \u2022 \u2022 \u2022";
            default:
                return string.Empty;
        }
    }

    public string GetProductionNumberDisplayState()
    {
        string[] labels = new string[productionNumberLabels.Length];
        for (int i = 0; i < productionNumberLabels.Length; i++)
        {
            labels[i] = productionNumberLabels[i] == null
                ? "<missing>"
                : productionNumberLabels[i].text;
        }

        return string.Join(", ", labels);
    }

    private readonly GameObject[] tokenRoots = new GameObject[3];
    private Quaternion _lastCamRot = Quaternion.identity;
    private bool _hasCamRot = false;

    private void LateUpdate()
    {
        Camera cam = UIManager.MainCamera;
        if (cam == null) return;
        Quaternion camRot = cam.transform.rotation;
        if (_hasCamRot && camRot == _lastCamRot) return;
        _lastCamRot = camRot;
        _hasCamRot = true;
        for (int i = 0; i < tokenRoots.Length; i++)
        {
            if (tokenRoots[i] != null)
            {
                tokenRoots[i].transform.rotation = camRot;
            }
        }
    }

    private void CreateProductionNumberChip(Transform planet, int number, int index, bool revealed)
    {
        if (tokenRoots[index] != null)
        {
            Destroy(tokenRoots[index]);
            tokenRoots[index] = null;
        }

        Vector3 chipPosition = planet.position + Vector3.up * 0.55f;

        // Container root dynamically billboarded toward camera
        GameObject tokenRoot = new GameObject("HoloTokenRoot_" + index);
        tokenRoot.transform.SetParent(transform, true);
        tokenRoot.transform.position = chipPosition;
        if (Camera.main != null)
        {
            tokenRoot.transform.rotation = Camera.main.transform.rotation;
        }
        else
        {
            tokenRoot.transform.rotation = Quaternion.Euler(68f, 0f, 0f);
        }

        Vector3 parentScale = transform.lossyScale;
        float invScale = parentScale.x != 0 ? 1f / parentScale.x : 1f;
        tokenRoot.transform.localScale = Vector3.one * invScale;
        tokenRoots[index] = tokenRoot;

        // Primary text (Main number or Hazard title)
        GameObject mainLabelObject = new GameObject(number.ToString());
        mainLabelObject.transform.SetParent(tokenRoot.transform, false);
        mainLabelObject.transform.localPosition = revealed ? Vector3.zero : new Vector3(0f, 0.032f, 0f);
        mainLabelObject.transform.localRotation = Quaternion.identity;

        TextMesh mainLabel = mainLabelObject.AddComponent<TextMesh>();
        mainLabel.text = revealed ? number.ToString() : "?";
        mainLabel.anchor = TextAnchor.MiddleCenter;
        mainLabel.alignment = TextAlignment.Center;
        mainLabel.fontSize = 72;
        mainLabel.characterSize = (number >= 10) ? 0.080f : 0.090f;
        mainLabel.fontStyle = FontStyle.Bold;
        mainLabel.color = HoloBorderNormal;
        mainLabel.GetComponent<Renderer>().sortingOrder = 2;

        // Secondary text (Hazard details or status)
        GameObject subLabelObject = new GameObject("SubLabel_" + index);
        subLabelObject.transform.SetParent(tokenRoot.transform, false);
        subLabelObject.transform.localPosition = new Vector3(0f, -0.045f, 0f);
        subLabelObject.transform.localRotation = Quaternion.identity;

        TextMesh subLabel = subLabelObject.AddComponent<TextMesh>();
        subLabel.text = revealed ? string.Empty : "[SCAN]";
        subLabel.anchor = TextAnchor.MiddleCenter;
        subLabel.alignment = TextAlignment.Center;
        subLabel.fontSize = 54;
        subLabel.characterSize = 0.038f;
        subLabel.fontStyle = FontStyle.Bold;
        subLabel.color = HoloBorderNormal;
        subLabel.GetComponent<Renderer>().sortingOrder = 2;

        productionNumberLabels[index] = mainLabel;
        productionNumberSubLabels[index] = subLabel;
    }

    private Transform GetChildByName(string childName)
    {
        return gameObject.transform.Find(childName);
    }
}
