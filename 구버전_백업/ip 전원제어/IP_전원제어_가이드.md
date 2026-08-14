# 로컬 네트워크를 통한 IP 전원 제어 기술 가이드 (PC & 프로젝터)

본 가이드는 시연실 통합 실행기(**Showroom Launcher**)의 확장 기능으로 도입 가능한 로컬 네트워크(LAN) 기반의 기기 제어(PC, 빔 프로젝터, 스마트 플러그 등) 방법론과 구체적인 구현 방식을 알기 쉽게 설명합니다.

---

## 1. 전원 제어 가능 여부

**결론부터 말씀드리면 100% 가능합니다.**

시연실이나 회의실 등에 배치된 전자기기가 로컬 네트워크(Wi-Fi 또는 유선 LAN)에 연결되어 있고, 고유의 IP 세팅을 할 수 있다면 다양한 네트워크 프로토콜(UDP, TCP, HTTP 등)을 사용해 원격으로 **전원 켜기(Power On)**, **전원 끄기(Power Off)**, 그리고 현재 **전원 상태 조회(Status Query)**를 수행할 수 있습니다.

---

## 2. 장비별 제어 방식 및 원리 설명

### 2.1 PC (데스크톱 및 서버)

PC는 완전히 꺼져 있을 때와 켜져 있을 때 제어 신호를 수신하는 주체가 다릅니다.

#### 1) PC 전원 켜기 (Power On) — **WOL (Wake-on-LAN)**

* **작동 원리**: PC 전원이 완전히 꺼져 있더라도 랜선(UTP)이 연결되어 있고 대기 전력이 공급되고 있다면, 메인보드와 네트워크 카드(NIC)는 초저전력 대기 상태를 유지합니다. 이때 로컬 네트워크망 전체에 기기의 물리적 주소(**MAC Address**)가 포함된 특수한 패킷(**Magic Packet**)을 브로드캐스트 전송하면 랜카드가 이를 감지하고 PC 전원을 켭니다.
* **패킷 구성**: 6바이트의 `0xFF` 데이터 뒤에 제어 대상 PC의 MAC 주소(6바이트)가 16번 연속으로 붙는 총 102바이트짜리 단순 패킷입니다.
* **선행 설정 필수**:
  1. **BIOS/UEFI 설정**: 메인보드 설정에서 `Wake on LAN`, `Power On By PCI-E Device`, `ERP Ready (Disabled)` 옵션 활성화.
  2. **Windows 랜카드 설정**: 장치 관리자 -> 네트워크 어댑터 -> 속성 -> 전원 관리 탭에서 "이 장치를 사용하여 컴퓨터를 깨울 수 있음" 체크.
* **C# 코드 구현 예시**:
  ```csharp
  using System;
  using System.Net;
  using System.Net.Sockets;

  public class WakeOnLan
  {
      public static void WakeUp(string macAddress)
      {
          // MAC 주소 포맷 정리 (하이픈, 콜론 제거)
          string cleanMac = macAddress.Replace("-", "").Replace(":", "");
          byte[] macBytes = new byte[6];
          for (int i = 0; i < 6; i++)
          {
              macBytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
          }

          // Magic Packet 구성 (0xFF * 6 + MAC * 16)
          byte[] packet = new byte[102];
          for (int i = 0; i < 6; i++) packet[i] = 0xFF;
          for (int i = 1; i <= 16; i++)
          {
              Array.Copy(macBytes, 0, packet, i * 6, 6);
          }

          // 로컬 네트워크 브로드캐스트 전송 (UDP 포트 7 or 9 주로 사용)
          using (UdpClient client = new UdpClient())
          {
              client.Connect(IPAddress.Broadcast, 9);
              client.Send(packet, packet.Length);
          }
      }
  }
  ```

#### 2) PC 전원 끄기 (Power Off) — **원격 종료 에이전트(Agent) 방식**

* **작동 원리**: 이미 켜져 있는 PC의 전원을 완전히 끄기 위해선 운영체제(OS) 수준에서 셧다운 시스템 명령어를 실행시켜야 합니다. 방화벽이나 권한 문제가 까다롭기 때문에 **가장 안정적이고 확실한 방법은 종료 대상 PC에 50줄짜리 초경량 백그라운드 리스너(Agent) 프로그램을 띄워두는 것**입니다.
* **흐름**:
  1. 통합 실행기(Showroom Launcher)가 대상 PC의 특정 포트(예: TCP 9999)로 `"SHUTDOWN"` 문자열을 전송합니다.
  2. 대상 PC에서 실행 중이던 에이전트 프로그램이 이 명령어를 감지하고 로컬 시스템 명령을 즉시 실행합니다.
  3. `System.Diagnostics.Process.Start("shutdown.exe", "-s -f -t 0");` (윈도우 강제 즉시 종료 명령어)
* **보안**: 무단 종료를 방지하기 위해 간단한 비밀번호(Pre-shared Key)나 암호 해시를 패킷에 섞어 검증하는 것이 안전합니다.

---

### 2.2 프로젝터 및 디스플레이 장비

상업용/전시용 빔 프로젝터(Epson, Optoma, Panasonic, Sony 등)는 네트워크 포트가 내장되어 있어 직접적인 통신 제어가 수월합니다.

#### 1) 산업 표준 프로토콜 — **PJLink**

* **개요**: 대부분의 글로벌 프로젝터 제조사가 공동 지원하는 표준 통신 규격입니다.
* **포트 및 프로토콜**: **TCP 포트 4352**를 사용하며 평문(ASCII텍스트) 또는 MD5 해시 보안 암호화로 통신합니다.
* **핵심 명령어**:
  * **전원 켜기**: `%1POWR 1\r`
  * **전원 끄기**: `%1POWR 0\r`
  * **상태 조회**: `%1POWR ?\r` (기기가 `0: 꺼짐`, `1: 켜짐`, `2: 쿨링 중`, `3: 워밍업 중`으로 답변함)
* **C# 코드 구현 예시 (일반 평문 모드 기준)**:
  ```csharp
  using System;
  using System.Net.Sockets;
  using System.Text;

  public class PJLinkControl
  {
      public static string SendCommand(string ipAddress, string command)
      {
          try
          {
              using (TcpClient client = new TcpClient(ipAddress, 4352))
              using (NetworkStream stream = client.GetStream())
              {
                  // 1. 기기가 보낸 접속 완료 환영 메시지(예: "PJLINK 0\r") 먼저 읽기
                  byte[] buffer = new byte[1024];
                  int bytesRead = stream.Read(buffer, 0, buffer.Length);
                  string welcome = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                  // 2. 명령어 송신 (예: "%1POWR 1\r")
                  byte[] cmdBytes = Encoding.ASCII.GetBytes(command);
                  stream.Write(cmdBytes, 0, cmdBytes.Length);

                  // 3. 실행 결과 수신 (예: "%1POWR=OK\r" 또는 "%1POWR=1\r")
                  bytesRead = stream.Read(buffer, 0, buffer.Length);
                  return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
              }
          }
          catch (Exception ex)
          {
              return $"Error: {ex.Message}";
          }
      }
  }
  ```

#### 2) 제조사 전용 프로토콜 (ESC/VP.net 등)

* PJLink 외에도 제조사에 따라 TCP/IP 소켓으로 다이렉트 텍스트 명령어를 받아 처리하기도 합니다.
  * *예시 (Epson 프로젝터)*: TCP 포트 3629로 연결하여 `PWR ON\r` 또는 `PWR OFF\r` 명령어를 바로 날리는 방식.
  * 장비 사양서(네트워크 제어 설명서)를 참조하여 제어 명령어 리스트를 확인하면 간단히 매핑할 수 있습니다.

---

### 2.3 기타 전자기기 (스마트 콘센트 / 스마트 플러그)

WOL이 불가능하고 별도 통신 카드도 없는 일반 가전기기(예: 구형 모니터, 대형 전광판, 단순 조명, 멀티미디어 앰프 등)의 경우, **스마트 멀티탭 및 스마트 플러그**를 로컬망에 붙여 전력 공급을 원격 차단/인가하는 방식을 씁니다.

* **작동 방식**:
  1. 시연실 Wi-Fi/LAN에 연결되는 스마트 플러그에 전자기기를 꽂습니다.
  2. 스마트 플러그 제조사에서 로컬 API(예: TP-Link Kasa Local API, Tuya CoAP 등)를 활용하여 콘센트 자체의 전원을 ON/OFF 함으로써 기기 전체의 물리 전원을 제어합니다.
* **특징**: 기기 자체를 개조하거나 복잡한 설정을 할 필요가 없는 가장 범용적이고 직관적인 전원 제어법입니다.

---

## 3. Showroom Launcher(통합 실행기) 아키텍처 연동 방안

현재 구동 중인 실행기의 기존 코드를 오염시키지 않으면서 이 기능을 유연하게 장착하는 설계 가이드라인입니다.

### 3.1 설정 파일(`config.json`) 확장 설계

기존 콘텐츠 목록 설정 외에 전원 제어 대상 장비들의 메타데이터 노드를 추가해 구성합니다.

```json
{
  "ContentItems": [
    // ... 기존 콘텐츠 설정
  ],
  "PowerDevices": [
    {
      "id": "showroom_projector",
      "name": "시연실 메인 프로젝터",
      "type": "PJLink",
      "ip": "192.168.0.50",
      "port": 4352
    },
    {
      "id": "interactive_pc",
      "name": "인터랙티브 체험 PC",
      "type": "WOL",
      "ip": "192.168.0.100",
      "mac": "AA-BB-CC-DD-EE-FF"
    }
  ]
}
```

### 3.2 UI 및 백그라운드 태스크 연동 흐름

1. **관리자 메뉴(Admin Control) 제공**:
   * 실행기 메인 화면의 구석 영역을 연속 클릭하거나 관리자 단축키(예: `Ctrl + Shift + A`)를 눌러 전원 설정 패널을 띄웁니다.
2. **일괄 제어(Macro) 실행**:
   * **전체 켜기**: 등록된 모든 WOL 장비에 매직 패킷을 동시 다발적으로 날리고, 빔 프로젝터에 PJLink 전원 켬 명령을 비동기(`Task.Run()`)로 순차 전송합니다.
   * **전체 끄기**: PC 에이전트들에 종료 신호를 보내 안전하게 끄고, 프로젝터에 PJLink 전원 끔 명령을 전송하여 장비 수명을 지키며 끕니다.
3. **실시간 모니터링 연동**:
   * 실행기가 켜져 있는 동안 백그라운드 타이머가 돌며 각 장비의 IP로 Ping 또는 PJLink 상태 쿼리를 던져 기기 활성화 상태를 초록(ON) / 빨강(OFF) 인디케이터로 실시간 표시해 줍니다.
