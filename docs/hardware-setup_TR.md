# Donanım bağlantıları

Aşağıdaki tablolar, paket içindeki firmware kodlarında kullanılan pin tanımlarıyla uyumludur.

## Arduino gerilim ve röle istasyonu

| İşlev | Arduino pini | Bağlantı |
|---|---:|---|
| Potansiyometre sinyali | `A0` | Orta uç; dış uçlar `5V` ve `GND` |
| K1 butonu | `D2` | `D2` ile `GND` arasına; `INPUT_PULLUP` olarak tanımlı ve mevcut kodda ayrılmış durumda |
| Sistem rölesi girişi | `D3` | Röle modülü 1. kanal girişi |
| Motor rölesi girişi | `D4` | Röle modülü 2. kanal girişi |
| Yeşil PASS LED'i | `D7` | LED anodu 220–330 Ω direnç üzerinden; katot `GND` |
| Kırmızı FAIL LED'i | `D8` | LED anodu 220–330 Ω direnç üzerinden; katot `GND` |
| Buzzer sinyali | `D9` | Aktif buzzer sinyal girişi |
| OLED SDA | Uno/Nano için `A4` | SSD1306 I²C veri hattı, adres `0x3C` |
| OLED SCL | Uno/Nano için `A5` | SSD1306 I²C saat hattı |
| OLED beslemesi | `5V` / `GND` | Kullanılan OLED modülünün besleme değerini doğrulayın |

### Potansiyometre bağlantısı

| Potansiyometre ucu | Bağlantı |
|---|---|
| Birinci dış uç | Arduino `5V` |
| Orta uç | Arduino `A0` |
| İkinci dış uç | Arduino `GND` |

### Röle çıkışları

Arduino kodu aktif-düşük röle modülü varsayar:

```text
LOW  = röle enerjili
HIGH = röle bırakılmış
```

Aktif-yüksek bir modül kullanılıyorsa Arduino kodundaki `RELAY_ON` ve `RELAY_OFF` tanımları değiştirilmelidir. Motor Arduino I/O pininden beslenmemelidir. Yüke uygun röle veya motor sürücüsü, gerekli harici güç kaynağı, ihtiyaç halinde ortak GND ve yüke uygun ters gerilim/sönümleme koruması kullanılmalıdır.

## ESP32 Wi-Fi kalite test düğümü

| İşlev | Bağlantı |
|---|---|
| Besleme ve programlama | ESP32 USB bağlantısı |
| Seri telemetri | USB sanal COM portu, `9600` baud |
| Wi-Fi ölçümü | Dahili 2,4 GHz radyo; kod yalnızca ağ taraması yapar |

## Bilgisayar bağlantısı

- Arduino ve ESP32 aynı anda bağlanabilir.
- Her cihaz için farklı bir COM portu seçilmelidir.
- Her iki bağlantı `9600` baud, 8 veri biti, eşlik biti yok ve 1 dur biti kullanır.
- HMI üzerindeki iki seçim alanında aynı COM portu seçilmemelidir.

## İlk test sırası

1. Motor veya yük beslemesini bağlı tutmayın.
2. Arduino kodunu yükleyip OLED, LED, buzzer ve röle göstergelerini kontrol edin.
3. Arduino COM portunu HMI üzerinden bağlayın.
4. 1,0–4,5 V gibi gerilim limitleri gönderin.
5. Potansiyometreyi düşük, PASS ve yüksek gerilim bölgelerinde deneyin.
6. E-STOP ve RESET davranışını doğrulayın.
7. ESP32'yi farklı bir COM portundan bağlayıp RSSI/ağ sayısı paketlerini kontrol edin.
8. Harici yükü yalnızca rölenin aktif ve pasif durumlarını doğruladıktan sonra bağlayın.
