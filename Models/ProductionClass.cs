using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CMKL.Models
{
    public class ProductionClass
    {
    }
    public class Voucher
    {
        public int Id { get; set; }
        public DateTime CompletionDate { get; set; }
        public string Status { get; set; }
        // Add other voucher properties
    }

    public class EngineData
    {
        //public int Id { get; set; }  //Added Id
        public int VoucherId { get; set; }
        public string emake { get; set; }
        public int emodel { get; set; }
        public string eserial { get; set; }
        public string etestcert { get; set; }
        public string eremarks { get; set; }
        // Add other engine properties
    }

    public class Alternator
    {
       
        public int VoucherId { get; set; }
        public int aphase { get; set; } 
        public int arating { get; set; }
        public string amake { get; set; }
        public string aframe { get; set; }
        public string amcNo { get; set; }
        public string atestCert { get; set; }
        public string awarranty { get; set; }
        public string aremarks { get; set; }
        // Add other alternator properties
    }

    public class OtherData
    {
        
        public int exhaust { get; set; }
        public string kgSticker { get; set; }
        public string avmPads { get; set; }
        public int fuelSpout { get; set; }
        public int flexBellow { get; set; }
        // Add other properties
    }
    public class TestingData
    {
        public int Id { get; set; }  //  Added Id to match your data structure
        public int CPtype { get; set; }
        public int CPRating { get; set; }
        public string PanelSerialNo { get; set; }
        public string CPOtherDetail { get; set; }
        public string BMake { get; set; }
        public int BatteryRating { get; set; }
        public int BatteryQTY { get; set; }
        public string BatterySerialNo { get; set; }      
        public DateTime DateOfTesting { get; set; } // Use DateTime
        public int RatingOfGenset { get; set; }
        public decimal Canopy { get; set; }
        public string Lube { get; set; }
        public string KCool { get; set; }
        public string KRMNo { get; set; }
        public string ADBlue { get; set; }
        public string SpecialRemarks { get; set; }
    }
}