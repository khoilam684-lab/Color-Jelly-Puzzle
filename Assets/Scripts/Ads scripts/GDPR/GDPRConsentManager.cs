using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Ump;
using GoogleMobileAds.Ump.Api;

public class GDPRConsentManager : MonoBehaviour //General Data Protection Regulation
{
#if !UNITY_EDITOR
    ConsentForm _consentForm;
    // Start is called before the first frame update
    void Start()
    {
        var debugSettings = new ConsentDebugSettings
        {
            // Geography appears as in EEA for debug devices. // khu vuc kin te chau au European Economic Area
            DebugGeography = DebugGeography.EEA,
            TestDeviceHashedIds = new List<string>
            {
                "ACCB85DC567CE5C24822C8E4C31C03ED"
            }
        };

        // Here false means users are not under age.
        // khong duoc hien thi quang cao duoc ca nhan hoa cho user <18 tuoi trong khu vuc EEA + US + ThuySi
        ConsentRequestParameters request = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = false,
            ConsentDebugSettings = debugSettings,
        };

        // Check the current consent information status.
        ConsentInformation.Update(request, OnConsentInfoUpdated);
    }

    void OnConsentInfoUpdated(FormError consentError)
    {
        if (consentError != null)
        {
            // Handle the error.
            UnityEngine.Debug.LogError(consentError);
            return;
        }

        if (ConsentInformation.IsConsentFormAvailable())
        {
            LoadConsentForm();
        }
        // If the error is null, the consent information state was updated.
        // You are now ready to check if a form is available.
    }

    void LoadConsentForm()
    {
        // Loads a consent form.
        ConsentForm.Load(OnLoadConsentForm);
    }

    void OnLoadConsentForm(ConsentForm consentForm, FormError formError)
    {
        if (formError != null)
        {
            // Handle the error.
            UnityEngine.Debug.LogError(formError);
            return;
        }

        // The consent form was loaded.
        // Save the consent form for future requests.
        _consentForm = consentForm;

        // You are now ready to show the form.
        if (ConsentInformation.ConsentStatus == ConsentStatus.Required)
        {
            _consentForm.Show(OnShowForm);
        }
    }


    void OnShowForm(FormError formError)
    {
        if (formError != null)
        {
            // Handle the error.
            UnityEngine.Debug.LogError(formError);
            return;
        }

        // Handle dismissal by reloading form.
        LoadConsentForm();
    }

#endif
}