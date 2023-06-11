window.kms = (() => {
    const submitForm = formId => document.getElementById(formId).submit()
    const breakReconnection = () => {
        Blazor.defaultReconnectionHandler.onConnectionDown = _ => {}   
        Blazor.disconnect()
    }
    const redirectTo = url => window.location.href = url
    return { breakReconnection,
             redirectTo,
             submitForm }
})()