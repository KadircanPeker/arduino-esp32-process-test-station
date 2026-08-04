using System;

namespace ProcessTestApp
{
    public class TestData
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; }
        public string ProductType { get; set; }
        public double Voltage { get; set; }
        public double Current { get; set; }
        public string Result { get; set; }
        public string ErrorCode { get; set; }
        public DateTime LogTime { get; set; }
        public int TestAttemptNo { get; set; }
        public string StationName { get; set; }
        public string OperatorName { get; set; }
        public string BatchNo { get; set; }
        public string SourceType { get; set; }
        public double MinLimit { get; set; }
        public double MaxLimit { get; set; }
    }

    public class ProductThreshold
    {
        public string ProductType { get; set; }
        public double MinVoltage { get; set; }
        public double MaxVoltage { get; set; }
        public double MinCurrent { get; set; }
        public double MaxCurrent { get; set; }
        public string IpcClass { get; set; }
    }
}
