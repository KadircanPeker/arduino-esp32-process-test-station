#include "WiFi.h"

int testCounter = 8000;
bool isScanning = false;

void setup() {
  Serial.begin(9600);
  delay(500);
  
  WiFi.mode(WIFI_STA);
  WiFi.disconnect();
  delay(100);
  
  Serial.println("SN8000;WIFI_TESTER;0.00;0.00;PASS;E00");
  
  // Asenkron (arka planda hızlı) Wi-Fi taramasını başlat
  WiFi.scanNetworks(true, true);
  isScanning = true;
}

void loop() {
  if (isScanning) {
    int n = WiFi.scanComplete();
    
    // n >= 0 ise tarama tamamlanmıştır (n = bulunan ağ sayısı)
    if (n >= 0) {
      testCounter++;
      
      if (n == 0) {
        Serial.println("SN" + String(testCounter) + ";WIFI_TESTER;99.00;0.00;FAIL;E05");
      } 
      else {
        int32_t strongestRSSI = WiFi.RSSI(0); 
        float signalStrength = abs(strongestRSSI); // Örn: -48 dBm -> 48.0
        float totalNetworks = (float)n;           // Bulunan toplam ağ sayısı
        
        String result = (signalStrength <= 75.0) ? "PASS" : "FAIL";
        String errorCode = (signalStrength <= 75.0) ? "E00" : "E05";
        
        String dataString = "SN" + String(testCounter) + ";WIFI_TESTER;" + 
                            String(signalStrength, 2) + ";" + 
                            String(totalNetworks, 2) + ";" + 
                            result + ";" + errorCode;
        
        Serial.println(dataString);
      }
      
      // Belleği temizle ve anında yeni asenkron taramayı başlat
      WiFi.scanDelete();
      WiFi.scanNetworks(true, true);
      isScanning = true;
    }
  }
  
  delay(30);
}
