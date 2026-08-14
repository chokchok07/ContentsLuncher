# [Part 3] 시스템 트레이, Mutex 중복 차단 및 자동 종료 스케줄러

런처의 비정상 종료를 예방하기 위한 시스템 트레이 전환 설계, 중복 실행 방지 Mutex 윈도우 메시지 복원 시스템, 스케줄러 기반의 프로세스 순차 클린업 및 시스템 종료 로직 기술 사양서입니다.

---

## 📥 1. ESC 트레이 최소화 강제 이동 구조
관리자가 런처 화면 내에서 ESC 키를 실수로 누르거나 키보드 오동작이 발생했을 때, 프로그램이 갑자기 꺼져 전체 콘텐츠 관리에 틈이 생기는 현상을 완전 차단합니다.

* **키 감지 핸들러 (`MainWindow_KeyDown`)**: 런처 창에 포커스가 유지된 상태에서 `KeyEventArgs`로 `Keys.Escape`를 검출합니다.
* **이벤트 가로채기 및 은닉**: 프로그램 종료 프로세스를 밟지 않고, `e.Handled = true`로 키 입력을 소멸시킨 뒤 즉시 `this.Hide()`를 실행하여 백그라운드 시스템 트레이 영역으로 런처 창을 안전하게 은닉(최소화) 시킵니다.

---

## 🛡️ 2. 단일 인스턴스 중복 차단 및 기존 인스턴스 복원
런처가 메모리에 중복 실행되어 포커스 고정 충돌 및 타이머 리소스 낭비가 초래되는 현상을 방지하는 고급 윈도우 IPC(Inter-Process Communication) 아키텍처입니다.

```mermaid
graph TD
    A[두 번째 Launcher.exe 실행 시도] --> B{Global Mutex가 이미 소유 중인가?}
    B -- YES --> C[윈도우 메시지 WM_SHOWME 등록 및 브로드캐스트]
    C --> D[기존 실행 중이던 Launcher.exe의 윈도우 핸들 탐색 FindWindow]
    D --> E[PostMessage를 통해 기존 핸들에 WM_SHOWME 전송]
    E --> F[기존 런처 창이 트레이에서 복원 ShowWindowAsync & SetForegroundWindow]
    F --> G[두 번째 Launcher.exe는 조용히 즉시 종료 return]
```

### 글로벌 Mutex 소유권 탐색
* 런처 엔트리 포인트(`Main()`) 시작 시 `System.Threading.Mutex`에 `"Global\\ShowroomLauncher_Unique_Mutex_Name"` 식별자를 주어 프로세스 유일성을 소유합니다.
* 이미 기존 인스턴스가 존재하면 새로 실행된 런처는 즉시 중복 실행 모드로 빠집니다.

### WM_SHOWME 윈도우 메시지 포스팅 복원
* 중복 인스턴스가 확인되면, 시스템 고유 윈도우 메시지 식별자인 `RegisterWindowMessage`를 통해 `"WM_SHOWME_SHOWROOM_LAUNCHER"` 메시지를 소환합니다.
* `FindWindow` 및 프로세스 탐색을 연동하여 시스템 트레이 뒤에 숨어있던 기존 런처의 윈도우 핸들(`IntPtr`)을 정밀 추적합니다.
* 추적된 기존 핸들로 `PostMessage` API를 통해 `WM_SHOWME` 메시지를 전송하고, 새 인스턴스는 즉시 스스로 소멸(`return`)합니다.
* 메시지를 전송받은 기존 런처는 트레이 은닉을 해제(`ShowWindowAsync` 호출 및 `SW_RESTORE`)하고 화면 최상단 전면(`SetForegroundWindow`)으로 당당히 복원되어 활성화됩니다.

---

## ⏰ 3. 글로벌 자동화 스케줄러 및 전원(PC 끄기) 제어
정해진 영업 마감 시각이나 특정 타이밍에 자동으로 모든 시연을 종료하고 PC 전원까지 안전하게 셧다운시키는 스마트 셧다운 시나리오입니다.

1. **글로벌 스케줄러 틱 동기화**: `statusTimer`가 1초마다 돌며 `schedulerEnabled` 상태와 등록된 마감 시간(`autoShutdownTime`, format: `HH:mm`)이 현재 시간과 일치하는지 정밀 진단합니다.
2. **콘텐츠 순차 클린업 (`KillAllActive`)**:
   * 지정 시간에 다다르는 즉시 기동 중인 모든 콘텐츠(`activeProcesses`)의 동작 스레드를 정지하고, 메인 및 모듈 하위 프로세스 트리까지 깔끔하게 파워 종료(`Kill`)시킵니다.
3. **윈도우 shutdown 전원 제어**:
   * '종료 시 PC도 끄기' 옵션(`isPcShutdown = true`)이 체크되어 있다면, OS 명령프롬프트를 경유해 `shutdown /s /t 30` 명령을 무소음 백그라운드 모드로 발사합니다.
   * 이에 따라 윈도우 OS는 30초의 유예 시간을 두고 본체 전원을 안전하게 셧다운시킵니다.

---

## 💾 4. BaseDirectory 귀속형 `config.json` 절대 경로 고정
배포 대상 PC의 실행 환경(작업 디렉토리 꼬임)이나 압축 프로그램 구동 폴더에 관계없이 설정 데이터를 안전하게 보존하기 위한 파일 입출력 아키텍처입니다.

* **상대 경로의 위험성**: 일반적인 `"config.json"` 상대 경로는 프로그램이 기동된 윈도우 작업 디렉토리 기준이므로, 읽기 전용 구역(압축 내부 임시 Temp 폴더 등)에서 런칭할 때 쓰기 권한 부족으로 인해 `Access Denied` 에러가 나서 런처 기능이 중단되었습니다.
* **절대 경로 고정**: 설정 파일의 물리 주소를 `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json")`으로 완전히 강제화했습니다. 
* 실행 파일(`Launcher.exe`)이 위치한 로컬 본인 디렉토리 영역만을 엄격하게 타겟팅하여 설정 파일을 열고 쓰므로, 배포 및 간접 호출 시 발생할 수 있는 보안 경로 충돌 문제를 영구적으로 해결했습니다.
