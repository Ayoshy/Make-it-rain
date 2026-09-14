#pragma once
#include <windows.h>
#include <winhttp.h>
#include <winrt/Windows.Data.Json.h>
#include <memory>
#include <atomic>
#include <cmath>
#include <limits>
#include <string>

struct DeskWeather {
    bool loaded=false,stale=false,day=true;
    double temperature=NAN,minimum=NAN,maximum=NAN,wind=NAN;
    int code=-1;
    std::wstring time;
};
inline std::wstring WeatherLabel(int code){
    if(code==0)return L"Ciel dégagé";if(code==1||code==2)return L"Éclaircies";if(code==3)return L"Nuageux";
    if(code==45||code==48)return L"Brouillard";if(code>=51&&code<=57)return L"Bruine";
    if((code>=61&&code<=67)||(code>=80&&code<=82))return L"Pluie";
    if((code>=71&&code<=77)||code==85||code==86)return L"Neige";if(code>=95&&code<=99)return L"Orage";
    return L"Conditions indisponibles";
}
inline std::wstring WeatherIcon(int code,bool day){
    if(code==0)return day?L"weather-sun":L"weather-moon";
    if(code>=1&&code<=3)return L"weather-cloud";if(code==45||code==48)return L"weather-fog";
    if((code>=51&&code<=67)||(code>=80&&code<=82))return L"weather-rain";
    if((code>=71&&code<=77)||code==85||code==86)return L"weather-snow";if(code>=95&&code<=99)return L"weather-storm";
    return L"weather-unknown";
}
inline DeskWeather ParseWeather(const std::wstring& body){
    using namespace winrt::Windows::Data::Json;
    auto json=JsonObject::Parse(body);auto current=json.GetNamedObject(L"current");DeskWeather result;
    result.temperature=current.GetNamedNumber(L"temperature_2m",NAN);
    if(!std::isfinite(result.temperature))throw std::runtime_error("Missing temperature");
    result.wind=current.GetNamedNumber(L"wind_speed_10m",NAN);result.code=(int)current.GetNamedNumber(L"weather_code",-1);
    result.day=current.GetNamedNumber(L"is_day",1)!=0;auto time=current.GetNamedString(L"time",L"");result.time=time.size()>=16?std::wstring(time).substr(11,5):L"";
    if(json.HasKey(L"daily")){auto daily=json.GetNamedObject(L"daily");
        auto get=[&](const wchar_t* key){auto a=daily.GetNamedArray(key,JsonArray{});return a.Size()&&a.GetAt(0).ValueType()==JsonValueType::Number?a.GetNumberAt(0):NAN;};
        result.minimum=get(L"temperature_2m_min");result.maximum=get(L"temperature_2m_max");}
    result.loaded=true;return result;
}
inline DeskWeather FetchWeather(const std::wstring& latitude,const std::wstring& longitude,std::atomic<HINTERNET>& activeRequest,std::atomic<bool>& stopping){
    using Handle=std::unique_ptr<void,decltype(&WinHttpCloseHandle)>;
    Handle session(WinHttpOpen(L"Battlestation/1.0",WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY,nullptr,nullptr,0),WinHttpCloseHandle);
    if(!session)throw std::runtime_error("HTTP unavailable");WinHttpSetTimeouts(session.get(),2500,2500,2500,2500);
    Handle connection(WinHttpConnect(session.get(),L"api.open-meteo.com",INTERNET_DEFAULT_HTTPS_PORT,0),WinHttpCloseHandle);
    auto path=L"/v1/forecast?latitude="+latitude+L"&longitude="+longitude+L"&current=temperature_2m,is_day,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&temperature_unit=celsius&wind_speed_unit=kmh&timezone=auto";
    auto request=WinHttpOpenRequest(connection.get(),L"GET",path.c_str(),nullptr,WINHTTP_NO_REFERER,WINHTTP_DEFAULT_ACCEPT_TYPES,WINHTTP_FLAG_SECURE);
    if(!request)throw std::runtime_error("Request unavailable");activeRequest=request;
    struct Cleanup{HINTERNET request;std::atomic<HINTERNET>& active;~Cleanup(){auto expected=request;if(active.compare_exchange_strong(expected,nullptr))WinHttpCloseHandle(request);}} cleanup{request,activeRequest};
    if(stopping||!WinHttpSendRequest(request,WINHTTP_NO_ADDITIONAL_HEADERS,0,WINHTTP_NO_REQUEST_DATA,0,0,0)||!WinHttpReceiveResponse(request,nullptr))throw std::runtime_error("Request failed");
    DWORD code=0,size=sizeof(code);WinHttpQueryHeaders(request,WINHTTP_QUERY_STATUS_CODE|WINHTTP_QUERY_FLAG_NUMBER,nullptr,&code,&size,nullptr);if(code!=200)throw std::runtime_error("Weather unavailable");
    std::string data;char buffer[8192];DWORD count=0;
    while(!stopping){if(!WinHttpReadData(request,buffer,sizeof(buffer),&count))throw std::runtime_error("Read failed");if(!count)break;data.append(buffer,count);if(data.size()>262144)throw std::runtime_error("Response too large");}
    if(stopping)throw std::runtime_error("Cancelled");return ParseWeather(winrt::to_hstring(data).c_str());
}
