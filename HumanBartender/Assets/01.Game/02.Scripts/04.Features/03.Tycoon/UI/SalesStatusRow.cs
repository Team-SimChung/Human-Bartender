using TMPro;
using UnityEngine;

public sealed class SalesStatusRow : MonoBehaviour
{
    [SerializeField] TMP_Text cocktailText;
    [SerializeField] TMP_Text gradeText;
    [SerializeField] TMP_Text amountText;

    public void Show(string cocktail, string grade, string amount)
    {
        cocktailText.text = cocktail;
        gradeText.text = grade;
        amountText.text = amount;
    }
}
