#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

#define SCREEN_WIDTH 128 
#define SCREEN_HEIGHT 64 
#define OLED_RESET    -1 
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

// --- PİN TANIMLAMALARI ---
const int buttonK1 = 2;       
const int potPin = A0;         
const int rolePin = 3;          // 5V Röle 1 (Sistem Güç Rölesi)
const int motorRolePin = 4;     // 5V Röle 2 (Motor Rölesi)
const int greenLedPin = 7;     
const int redLedPin = 8;       
const int buzzerPin = 9;       

// --- RÖLE TETİKLEME MANTIĞI ---
// Standart 5V röle kartları Active-LOW (0V ile çeken) çalışır.
// Eğer röleniz 5V ile çekiyorsa RELAY_ON değerini HIGH yapabilirsiniz.
#define RELAY_ON  LOW
#define RELAY_OFF HIGH

int testCounter = 7000;        
bool hasOled = true;

// C#'tan gelen acil stop kilidi
bool isEmergencyStop = false;  

// C#'tan dinamik olarak güncellenecek voltaj limitleri
float minVoltageLimit = 1.00;
float maxVoltageLimit = 4.50;

unsigned long lastTestTime = 0;
const unsigned long testInterval = 1200; // 1.2 saniyede bir ölçüm

void setup() {
  Serial.begin(9600); 
  
  pinMode(buttonK1, INPUT_PULLUP);
  pinMode(rolePin, OUTPUT);
  pinMode(motorRolePin, OUTPUT);
  pinMode(greenLedPin, OUTPUT);
  pinMode(redLedPin, OUTPUT);
  pinMode(buzzerPin, OUTPUT);

  // Açılışta röleleri başlangıç moduna al
  digitalWrite(rolePin, RELAY_ON); 
  digitalWrite(motorRolePin, RELAY_ON); 

  digitalWrite(greenLedPin, HIGH);
  digitalWrite(redLedPin, HIGH);
  tone(buzzerPin, 1500, 100); 
  delay(200);
  digitalWrite(greenLedPin, LOW);
  digitalWrite(redLedPin, LOW);
  
  if(!display.begin(SSD1306_SWITCHCAPVCC, 0x3C)) { 
    hasOled = false; 
  }

  if (hasOled) {
    display.clearDisplay();
    display.setTextSize(1);
    display.setTextColor(SSD1306_WHITE);
    display.setCursor(0, 20);
    display.println(F("SISTEM HAZIR"));
    display.setCursor(0, 35);
    display.println(F("C# BAGLANTISI BEKLENIYOR"));
    display.display();
    delay(1000);
  }
}

void loop() {
  // --- 1. C#'TAN GELEN KOMUTLARI DİNLE (ACİL STOP & LİMİT SENKRONİZASYONU) ---
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    command.trim(); 

    if (command == "E_STOP") {
      isEmergencyStop = true;
      tone(buzzerPin, 1000, 1000); 
    } 
    else if (command == "RESET") {
      isEmergencyStop = false;
      testCounter++; 
      tone(buzzerPin, 2000, 200); 
    }
    else if (command.startsWith("LIMITS;")) {
      int firstSemi = command.indexOf(';');
      int secondSemi = command.indexOf(';', firstSemi + 1);
      
      if (firstSemi != -1 && secondSemi != -1) {
        String minVStr = command.substring(firstSemi + 1, secondSemi);
        String maxVStr = command.substring(secondSemi + 1);
        
        minVStr.replace(',', '.');
        maxVStr.replace(',', '.');
        
        float newMin = minVStr.toFloat();
        float newMax = maxVStr.toFloat();
        
        if (newMin >= 0.0 && newMax > newMin) {
          minVoltageLimit = newMin;
          maxVoltageLimit = newMax;
          tone(buzzerPin, 1800, 150); 
        }
      }
    }
  }

  // --- 2. ACİL DURUM KİLİDİ ---
  if (isEmergencyStop) {
    digitalWrite(rolePin, RELAY_OFF);      // Gücü kes
    digitalWrite(motorRolePin, RELAY_OFF); // Motoru (Bandı) durdur
    digitalWrite(redLedPin, HIGH);
    digitalWrite(greenLedPin, LOW);

    if (hasOled) {
      display.clearDisplay();
      display.setTextSize(2);
      display.setCursor(0, 10);
      display.println(F("ACIL STOP"));
      display.setTextSize(1);
      display.setCursor(0, 40);
      display.println(F("C# Uzerinden"));
      display.setCursor(0, 50);
      display.println(F("Kilitlendi!"));
      display.display();
    }
    
    Serial.println("SN" + String(testCounter) + ";VOLTAGE_RELAY_TESTER;0.00;0.00;FAIL;E99");
    delay(500);
    return; 
  }

  // --- 3. TEST DÖNGÜSÜ ---
  if (millis() - lastTestTime >= testInterval) {
    lastTestTime = millis();

    int rawAnalog = analogRead(potPin);
    float voltage = (rawAnalog / 1023.0) * 5.0; 
    float current = (rawAnalog / 1023.0) * 2.5; 

    String result = "PASS";
    String errorCode = "E00";

    if (voltage > maxVoltageLimit) {
      result = "FAIL";
      errorCode = "E02"; 
    } else if (voltage < minVoltageLimit) {
      result = "FAIL";
      errorCode = "E01"; 
    }

    // 5V Çift Röle ve LED Kontrolü
    if (result == "FAIL") {
      digitalWrite(rolePin, RELAY_OFF);      
      digitalWrite(motorRolePin, RELAY_OFF); // FAIL durumunda motoru durdur
      digitalWrite(redLedPin, HIGH);
      digitalWrite(greenLedPin, LOW);
      tone(buzzerPin, 800, 300); 
    } else {
      digitalWrite(rolePin, RELAY_ON);       
      digitalWrite(motorRolePin, RELAY_ON);  // PASS durumunda motoru çalıştır
      digitalWrite(greenLedPin, HIGH);
      digitalWrite(redLedPin, LOW);
    }

    // C#'a verileri gönder (SRAM dostu - String obje hatası önlendi)
    Serial.print(F("SN"));
    Serial.print(testCounter);
    Serial.print(F(";VOLTAGE_RELAY_TESTER;"));
    Serial.print(voltage, 2);
    Serial.print(F(";"));
    Serial.print(current, 2);
    Serial.print(F(";"));
    Serial.print(result);
    Serial.print(F(";"));
    Serial.println(errorCode); 

    // Canlı OLED Güncellemesi
    if (hasOled) {
      display.clearDisplay();
      display.setTextSize(1);
      display.setCursor(0, 0);
      display.print(F("SN: ")); display.println(testCounter);
      display.print(F("Volt: ")); display.print(voltage, 2); display.println(F(" V"));
      display.print(F("Limit: ")); display.print(minVoltageLimit, 1); display.print(F("-")); display.print(maxVoltageLimit, 1); display.println(F("V"));
      display.print(F("Sonuc: ")); display.print(result); display.print(F(" (")); display.print(errorCode); display.println(F(")"));
      display.print(F("Bant : ")); display.println(result == "PASS" ? F("DONUYOR") : F("STOPPED"));
      display.display();
    }

    // Bir sonraki cihaz testi için seri numarasını arttır (Hem PASS hem FAIL için)
    testCounter++; 
  }
}
