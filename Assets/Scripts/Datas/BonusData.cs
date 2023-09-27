using System.Collections.Generic;
using UnityEngine;  // using for PlayerPrefs
using System;  // using for Serializable

[Serializable]
public class BonusData { 
    /* Type of this bonus is using for what character : Police or Thief */
    public BonusType bonusType = BonusType.Police;
    /* This value using to represents the value of increase and decrease. eg: Police speed increase [value], Thief increase point equal [value] */
    public int value = 1;
}