window.kms = (() => {
    const submitForm = formId => document.getElementById(formId).submit()
    const breakReconnection = () => {
        Blazor.defaultReconnectionHandler.onConnectionDown = _ => {}   
        Blazor.disconnect()
    }
    return { submitForm,
             breakReconnection }
})()