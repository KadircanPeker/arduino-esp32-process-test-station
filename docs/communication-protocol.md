# Serial communication protocol

Arduino and ESP32 use independent newline-delimited ASCII serial connections at `9600` baud.

## Device-to-HMI packet

```text
SerialNumber;ProductType;PrimaryValue;SecondaryValue;Result;ErrorCode
```

The packet contains exactly six semicolon-separated fields.

| Field | Arduino | ESP32 | Description |
|---|---|---|---|
| `SerialNumber` | `SN7015` | `SN8012` | Device-generated test identifier |
| `ProductType` | `VOLTAGE_RELAY_TESTER` | `WIFI_TESTER` | Measurement type |
| `PrimaryValue` | `2.82` | `37.00` | Voltage in V or positive RSSI magnitude |
| `SecondaryValue` | `1.41` | `16.00` | Derived current in A or network count |
| `Result` | `PASS` | `PASS` | Device-side decision |
| `ErrorCode` | `E00` | `E00` | Device-side diagnostic code |

Examples:

```text
SN7015;VOLTAGE_RELAY_TESTER;2.82;1.41;PASS;E00
SN7016;VOLTAGE_RELAY_TESTER;0.72;0.36;FAIL;E01
SN8012;WIFI_TESTER;37.00;16.00;PASS;E00
SN8013;WIFI_TESTER;82.00;3.00;FAIL;E05
```

Numeric values use `.` as the decimal separator.

## HMI-to-device commands

| Command | Arduino behavior | ESP32 behavior |
|---|---|---|
| `LIMITS;<min>;<max>` | Updates voltage limits | Not processed by the current ESP32 sketch |
| `E_STOP` | Latches the software stop and releases relay outputs | Not processed by the current ESP32 sketch |
| `RESET` | Clears the software stop | Not processed by the current ESP32 sketch |

Examples:

```text
LIMITS;1.00;4.50
E_STOP
RESET
```

## Result evaluation

The desktop application parses the device packet and evaluates the measurement again using the active HMI limits before writing the result to SQL Server.

- Arduino: below the lower limit → `E01`; above the upper limit → `E02`; inside the range → `E00`.
- ESP32: RSSI magnitude less than or equal to the configured magnitude passes. For example, `37 ≤ 75` represents `-37 dBm ≥ -75 dBm`.

## Error codes

| Code | Description |
|---|---|
| `E00` | Test passed / no error |
| `E01` | Voltage below lower limit |
| `E02` | Voltage above upper limit |
| `E05` | Wi-Fi not found or RSSI below the configured criterion |
| `E99` | Software emergency-stop state |
| `FORMAT_ERR` | Packet does not match the six-field format |
| `SQL_ERR` | Test result could not be stored in SQL Server |
