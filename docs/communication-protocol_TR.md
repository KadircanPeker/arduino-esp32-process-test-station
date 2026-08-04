# Seri haberleşme protokolü

Arduino ve ESP32, `9600` baud hızında birbirinden bağımsız ve satır sonuyla ayrılan ASCII seri bağlantılar kullanır.

## Cihazdan HMI'ya veri paketi

```text
SeriNumarası;ÜrünTipi;BirincilDeğer;İkincilDeğer;Sonuç;HataKodu
```

Paket, noktalı virgülle ayrılmış tam olarak altı alandan oluşur.

| Alan | Arduino | ESP32 | Açıklama |
|---|---|---|---|
| `SeriNumarası` | `SN7015` | `SN8012` | Cihazın oluşturduğu test kimliği |
| `ÜrünTipi` | `VOLTAGE_RELAY_TESTER` | `WIFI_TESTER` | Ölçüm türü |
| `BirincilDeğer` | `2.82` | `37.00` | Volt cinsinden gerilim veya pozitif RSSI büyüklüğü |
| `İkincilDeğer` | `1.41` | `16.00` | Türetilmiş akım veya ağ sayısı |
| `Sonuç` | `PASS` | `PASS` | Cihaz tarafındaki karar |
| `HataKodu` | `E00` | `E00` | Cihaz tarafındaki tanı kodu |

Örnekler:

```text
SN7015;VOLTAGE_RELAY_TESTER;2.82;1.41;PASS;E00
SN7016;VOLTAGE_RELAY_TESTER;0.72;0.36;FAIL;E01
SN8012;WIFI_TESTER;37.00;16.00;PASS;E00
SN8013;WIFI_TESTER;82.00;3.00;FAIL;E05
```

Sayısal alanlarda ondalık ayırıcı olarak `.` kullanılır.

## HMI'dan cihaza komutlar

| Komut | Arduino davranışı | ESP32 davranışı |
|---|---|---|
| `LIMITS;<min>;<max>` | Gerilim limitlerini günceller | Mevcut ESP32 kodunda işlenmez |
| `E_STOP` | Yazılımsal durdurmayı kilitler ve röle çıkışlarını bırakır | Mevcut ESP32 kodunda işlenmez |
| `RESET` | Yazılımsal durdurma kilidini kaldırır | Mevcut ESP32 kodunda işlenmez |

Örnekler:

```text
LIMITS;1.00;4.50
E_STOP
RESET
```

## Sonuç değerlendirmesi

Masaüstü uygulaması cihaz paketini ayrıştırır ve SQL Server'a yazmadan önce ölçümü etkin HMI limitleriyle tekrar değerlendirir.

- Arduino: alt limitin altında → `E01`; üst limitin üzerinde → `E02`; aralık içinde → `E00`.
- ESP32: RSSI büyüklüğü, ayarlanan büyüklük değerine eşit veya küçükse test geçer. Örneğin `37 ≤ 75`, `-37 dBm ≥ -75 dBm` anlamına gelir.

## Hata kodları

| Kod | Açıklama |
|---|---|
| `E00` | Test başarılı / hata yok |
| `E01` | Gerilim alt limitin altında |
| `E02` | Gerilim üst limitin üzerinde |
| `E05` | Wi-Fi bulunamadı veya RSSI belirlenen kriterin altında |
| `E99` | Yazılımsal acil durdurma durumu |
| `FORMAT_ERR` | Paket altı alanlı biçimle eşleşmiyor |
| `SQL_ERR` | Test sonucu SQL Server'a kaydedilemedi |
