# Hardware setup

The following tables match the pin definitions in the included firmware.

## Arduino voltage and relay station

| Function | Arduino pin | Connection |
|---|---:|---|
| Potentiometer signal | `A0` | Potentiometer wiper; outer terminals to `5V` and `GND` |
| K1 button | `D2` | Between `D2` and `GND`; configured as `INPUT_PULLUP` and reserved in the current firmware |
| System relay input | `D3` | Relay module channel 1 input |
| Motor relay input | `D4` | Relay module channel 2 input |
| Green PASS LED | `D7` | LED anode through a 220–330 Ω resistor; cathode to `GND` |
| Red FAIL LED | `D8` | LED anode through a 220–330 Ω resistor; cathode to `GND` |
| Buzzer signal | `D9` | Active buzzer signal input |
| OLED SDA | `A4` on Uno/Nano | SSD1306 I²C data, address `0x3C` |
| OLED SCL | `A5` on Uno/Nano | SSD1306 I²C clock |
| OLED supply | `5V` / `GND` | Confirm the voltage rating of the OLED module |

### Potentiometer

| Terminal | Connection |
|---|---|
| First outer terminal | Arduino `5V` |
| Middle terminal / wiper | Arduino `A0` |
| Second outer terminal | Arduino `GND` |

### Relay outputs

The sketch assumes an active-low relay module:

```text
LOW  = relay energized
HIGH = relay released
```

If an active-high module is used, update `RELAY_ON` and `RELAY_OFF` in the Arduino sketch. Do not power a motor from an Arduino I/O pin. Use a load-rated relay or motor driver, a suitable external supply, common ground where required, and flyback/suppression protection appropriate for the load.

## ESP32 Wi-Fi quality tester

| Function | Connection |
|---|---|
| Power and programming | ESP32 USB connector |
| Serial telemetry | USB virtual COM port, `9600` baud |
| Wi-Fi measurement | On-board 2.4 GHz radio; the sketch performs network scanning only |

## PC connection

- Arduino and ESP32 can be connected at the same time.
- Select a different COM port for each device.
- Both serial connections use `9600` baud, 8 data bits, no parity, and one stop bit.
- Do not select the same COM port in both HMI selectors.

## Initial test sequence

1. Keep the motor/load supply disconnected.
2. Upload the Arduino firmware and verify the OLED, LED, buzzer, and relay indicators.
3. Connect the Arduino COM port in the HMI.
4. Send voltage limits such as 1.0–4.5 V.
5. Rotate the potentiometer through low, PASS, and high regions.
6. Verify E-STOP and RESET behavior.
7. Connect the ESP32 on a separate COM port and verify RSSI/network-count packets.
8. Connect the external load only after confirming the relay's active and inactive states.
