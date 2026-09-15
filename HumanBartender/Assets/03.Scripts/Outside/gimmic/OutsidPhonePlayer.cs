using TMPro;
using UnityEngine;

public class OutsidePhonePlayer : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _Text;
    private string Myval;

    public void addVal(string val)
    {
        Debug.Log($"{val} 출력");
        Myval += val;
        _Text.text = Myval;
    }
}
